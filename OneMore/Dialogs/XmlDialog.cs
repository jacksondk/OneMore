﻿//************************************************************************************************
// Copyright © 2016 Steven M Cohn. All rights reserved.
//************************************************************************************************

#pragma warning disable CS3001  // Type is not CLS-compliant

namespace River.OneMoreAddIn.Dialogs
{
	using Microsoft.Office.Interop.OneNote;
	using System;
	using System.Collections.Generic;
	using System.Drawing;
	using System.Linq;
	using System.Windows.Forms;
	using System.Xml.Linq;
	using Resx = River.OneMoreAddIn.Properties.Resources;


	/// <summary>
	/// A dialog to view page and hierarchy XML and update page XML if desired.
	/// </summary>
	internal partial class XmlDialog : LocalizableForm
	{

		private ApplicationManager manager;
		private readonly ILogger logger;
		private int findIndex = -1;


		public XmlDialog()
		{
			InitializeComponent();

			if (DesignMode)
			{
				AutoScaleDimensions = new SizeF(96f, 96f);
				AutoScaleMode = AutoScaleMode.Dpi;
				Logger.SetDesignMode(DesignMode);
			}

			if (NeedsLocalizing())
			{
				Text = Resx.XmlDialog_Text;

				Localize(new string[]
				{
					"wrapBox",
					"selectButton",
					"hideBox",
					"hideLFBox",
					"pageTab",
					"hierTab",
					"notebooksHierButton",
					"sectionsHierButton",
					"pagesHierButton",
					"currNotebookButton",
					"currSectionButton",
					"pageNameLabel",
					"pagePathLabel",
					"pageLinkLabel",
					"okButton",
					"cancelButton"
				});
			}

			logger = Logger.Current;

			Width = Math.Min(2000, (int)(Screen.PrimaryScreen.WorkingArea.Width * 0.8));
			Height = Math.Min(1500, (int)(Screen.PrimaryScreen.WorkingArea.Height * 0.8));
		}


		private void MainForm_Load(object sender, EventArgs e)
		{
			manager = new ApplicationManager();

			// build pageInfoBox with custom order
			var names = new List<string>
			{
				Enum.GetName(typeof(PageInfo), PageInfo.piAll),
				Enum.GetName(typeof(PageInfo), PageInfo.piSelection),
				Enum.GetName(typeof(PageInfo), PageInfo.piBasic),
				Enum.GetName(typeof(PageInfo), PageInfo.piBinaryData),
				Enum.GetName(typeof(PageInfo), PageInfo.piBinaryDataSelection),
				Enum.GetName(typeof(PageInfo), PageInfo.piBinaryDataFileType),
				Enum.GetName(typeof(PageInfo), PageInfo.piFileType),
				Enum.GetName(typeof(PageInfo), PageInfo.piSelectionFileType)
			};

			pageInfoBox.Items.AddRange(names.ToArray());
			pageInfoBox.SelectedIndex = names.IndexOf("piSelection");

			// populate page info...
			var info = manager.GetCurrentPageInfo();
			pageName.Text = info.Name;
			pagePath.Text = info.Path;
			pageLink.Text = info.Link;
		}


		protected override void OnShown(EventArgs e)
		{
			//Location = new System.Drawing.Point(30, 30);
			UIHelper.SetForegroundWindow(this);
			findBox.Focus();
		}


		private void Close(object sender, EventArgs e)
		{
			manager.Dispose();
			Close();
		}


		// = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
		// Search, Wrap, Select

		private void ChangeFindText(object sender, EventArgs e)
		{
			if (findBox.Text.Length == 0)
			{
				findIndex = -1;
				findButton.Enabled = false;
			}
			else if (!findButton.Enabled)
			{
				findIndex = -1;
				findButton.Enabled = true;
			}
		}


		private void ClickFind(object sender, EventArgs e)
		{
			var box = tabs.SelectedIndex == 0 ? pageBox : hierBox;
			var index = FindNext(box, findBox.Text);

			if (index > 0)
			{
				box.Select(index, findBox.Text.Length);
				box.Focus();
				findIndex = index;
			}
		}


		private int FindNext(RichTextBox box, string text)
		{
			var index = box.Find(text, findIndex + 1, RichTextBoxFinds.None);

			if ((index < 0) && (findIndex > 0))
			{
				// wrap back to top and try again
				findIndex = -1;
				index = box.Find(text, 0, RichTextBoxFinds.None);
			}

			return index;
		}


		private void FindBoxKeyUP(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Enter)
			{
				ClickFind(sender, e);
				e.Handled = true;
			}
		}


		private void XmlBoxKeyUp(object sender, KeyEventArgs e)
		{
			if (e.Control && (e.KeyCode == Keys.F))
			{
				findBox.Focus();
			}
		}


		protected override bool ProcessDialogKey(Keys keyData)
		{
			if (keyData == (Keys.F | Keys.Control))
			{
				findBox.SelectAll();
				findBox.Focus();
				return true;
			}
			else if (keyData == Keys.F3)
			{
				ClickFind(null, null);
			}

			return base.ProcessDialogKey(keyData);
		}


		private void ChangeWrap(object sender, EventArgs e)
		{
			if (tabs.SelectedIndex == 0)
			{
				pageBox.WordWrap = wrapBox.Checked;
			}
			else
			{
				hierBox.WordWrap = wrapBox.Checked;
			}
		}


		private void SelectAll(object sender, EventArgs e)
		{
			if (tabs.SelectedIndex == 0)
			{
				pageBox.SelectAll();
				pageBox.Focus();
			}
			else
			{
				hierBox.SelectAll();
				hierBox.Focus();
			}
		}


		// = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
		// Page control

		private void ChangeInfoScope(object sender, EventArgs e)
		{
			if (Enum.TryParse<PageInfo>(pageInfoBox.Text, out var info))
			{
				var page = manager.CurrentPage(info);
				if (page != null)
				{
					var xml = page.ToString(SaveOptions.None);
					pageBox.Text = xml;

					ApplyHideOptions();

					logger.WriteLine($"XmlDialog loaded page, scope {info}, {xml.Length} chars");
				}
			}
		}


		private void HideAttributes(object sender, EventArgs e)
		{
			ChangeInfoScope(sender, e);

			if (hideBox.Checked || hideLFBox.Checked)
			{
				ApplyHideOptions();
			}

			okButton.Enabled = !hideBox.Checked;
		}


		private void ApplyHideOptions()
		{
			var root = XElement.Parse(pageBox.Text);

			if (hideBox.Checked)
			{
				// EditedByAttributes and others
				root.Descendants().Attributes().Where(a =>
					a.Name.LocalName == "author"
					|| a.Name.LocalName == "authorInitials"
					|| a.Name.LocalName == "authorResolutionID"
					|| a.Name.LocalName == "lastModifiedBy"
					|| a.Name.LocalName == "lastModifiedByInitials"
					|| a.Name.LocalName == "lastModifiedByResolutionID"
					|| a.Name.LocalName == "creationTime"
					|| a.Name.LocalName == "lastModifiedTime"
					|| a.Name.LocalName == "objectID")
					.Remove();
			}

			if (hideLFBox.Checked)
			{
				var nodes = root.DescendantNodes().OfType<XCData>();
				if (!nodes.IsNullOrEmpty())
				{
					foreach (var cdata in nodes)
					{
						cdata.Value = cdata.Value
							.Replace("\nstyle", " style")
							.Replace("\nhref", " href")
							.Replace(";\nfont-size:", ";font-size:")
							.Replace(";\ncolor:", ";color:")
							.Replace(":\n", ": ");
					}
				}
			}

			pageBox.Text = root.ToString(SaveOptions.None);
		}


		// = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
		// Tabs

		private void ChangeSelectedTab(object sender, EventArgs e)
		{
			if (tabs.SelectedIndex == 0)
			{
				pageBox.Select(0, 0);
				pageBox.Focus();
				okButton.Visible = true;
				pageInfoPanel.Visible = true;
				wrapBox.Checked = pageBox.WordWrap;
			}
			else
			{
				if (hierBox.TextLength == 0)
				{
					ShowHierarchy(HierarchyScope.hsNotebooks);
				}

				pageBox.Select(0, 0);
				pageBox.Focus();
				okButton.Visible = false;
				pageInfoPanel.Visible = false;
				wrapBox.Checked = hierBox.WordWrap;
			}
		}


		private void ShowNotebooks(object sender, EventArgs e)
		{
			ShowHierarchy(HierarchyScope.hsNotebooks);
		}

		private void ShowSections(object sender, EventArgs e)
		{
			ShowHierarchy(HierarchyScope.hsSections);
		}

		private void ShowPages(object sender, EventArgs e)
		{
			ShowHierarchy(HierarchyScope.hsPages);
		}

		private void ShowCurrentNotebook(object sender, EventArgs e)
		{
			var element = manager.CurrentNotebook();
			if (element != null)
			{
				var xml = element.ToString(SaveOptions.None);
				hierBox.Text = xml;
			}
			else
			{
				hierBox.Text = "Cannot get current notebook hierarchy";
			}
		}

		private void ShowCurrentSection(object sender, EventArgs e)
		{
			var element = manager.CurrentSection();
			if (element != null)
			{
				var xml = element.ToString(SaveOptions.None);
				hierBox.Text = xml;
			}
			else
			{
				hierBox.Text = "Cannot get current section hierarchy";
			}
		}


		private void ShowHierarchy(HierarchyScope scope)
		{
			var element = manager.GetHierarchy(scope);
			if (element != null)
			{
				var xml = element.ToString(SaveOptions.None);
				hierBox.Text = xml;
			}
			else
			{
				hierBox.Text = $"Cannot get hierarchy for {scope}";
			}
		}


		// = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
		// Update

		private void Update(object sender, EventArgs e)
		{
			var result = MessageBox.Show(
				"Are you sure? This may corrupt the current page.",
				"Feelin lucky punk?",
				MessageBoxButtons.OKCancel,
				MessageBoxIcon.Warning);

			if (result == DialogResult.OK)
			{
				try
				{
					var page = XElement.Parse(pageBox.Text);
					manager.UpdatePageContent(page);
					Close();
				}
				catch (Exception exc)
				{
					logger.WriteLine("Error updating page content", exc);
				}
			}
		}
	}
}
