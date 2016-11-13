﻿/* 
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using FlowSharpLib;

namespace FlowSharp
{
	public partial class FlowSharpUI
	{
		protected string filename;

		private void mnuTopmost_Click(object sender, EventArgs e)
		{
			canvasController.Topmost();
		}

		private void mnuBottommost_Click(object sender, EventArgs e)
		{
			canvasController.Bottommost();
		}

		private void mnuMoveUp_Click(object sender, EventArgs e)
		{
			canvasController.MoveSelectedElementsUp();
		}

		private void mnuMoveDown_Click(object sender, EventArgs e)
		{
			canvasController.MoveSelectedElementsDown();
		}

		private void mnuCopy_Click(object sender, EventArgs e)
		{
			if (canvasController.SelectedElements.Count > 0)
			{
				Copy();
			}
		}

		private void mnuPaste_Click(object sender, EventArgs e)
		{
			Paste();
		}

		private void mnuDelete_Click(object sender, EventArgs e)
		{
			Delete();
		}

		private void mnuNew_Click(object sender, EventArgs e)
		{
            if (CheckForChanges()) return;
            canvasController.Clear();
			canvas.Invalidate();
			filename = String.Empty;
			UpdateCaption();
            canvasController.UndoStack.ClearStacks();
        }

        private void mnuOpen_Click(object sender, EventArgs e)
		{
            if (CheckForChanges()) return;
			OpenFileDialog ofd = new OpenFileDialog();
			ofd.Filter = "FlowSharp (*.fsd)|*.fsd";
			DialogResult res = ofd.ShowDialog();

			if (res == DialogResult.OK)
			{
				filename = ofd.FileName;
			}
			else
			{
				return;
			}

			string data = File.ReadAllText(filename);
			List<GraphicElement> els = Persist.Deserialize(canvas, data);
            canvasController.Clear();
            canvasController.AddRange(els);
            canvasController.Elements.ForEach(el => el.UpdatePath());
			canvas.Invalidate();
			UpdateCaption();
            canvasController.UndoStack.ClearStacks();
        }

        private void mnuImport_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "FlowSharp (*.fsd)|*.fsd";
            DialogResult res = ofd.ShowDialog();

            if (res == DialogResult.OK)
            {
                string importFilename = ofd.FileName;
                string data = File.ReadAllText(importFilename);
                List<GraphicElement> els = Persist.Deserialize(canvas, data);
                canvasController.AddRange(els);
                canvasController.Elements.ForEach(el => el.UpdatePath());
                els.ForEach(el => canvas.Controller.SelectElement(el));
                canvas.Invalidate();
            }
        }

        private void mnuSave_Click(object sender, EventArgs e)
		{
			if (canvasController.Elements.Count > 0)
			{
                SaveOrSaveAs();
				UpdateCaption();
			}
			else
			{
				MessageBox.Show("Nothing to save.", "Empty Canvas", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
		}

		private void mnuSaveAs_Click(object sender, EventArgs e)
		{
			if (canvasController.Elements.Count > 0)
			{
                SaveOrSaveAs(true);
                UpdateCaption();
			}
			else
			{
				MessageBox.Show("Nothing to save.", "Empty Canvas", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
		}

		private void mnuExit_Click(object sender, EventArgs e)
		{
            if (CheckForChanges()) return;
            Close();
		}

        private void mnuGroup_Click(object sender, EventArgs e)
        {
            if (canvasController.SelectedElements.Any())
            {
                List<GraphicElement> selectedShapes = canvasController.SelectedElements.ToList();
                FlowSharpLib.GroupBox groupBox = new FlowSharpLib.GroupBox(canvas);

                canvasController.UndoStack.UndoRedo("Group",
                    () =>
                    {
                        ElementCache.Instance.Remove(groupBox);
                        canvasController.GroupShapes(groupBox);
                        canvasController.DeselectCurrentSelectedElements();
                        canvasController.SelectElement(groupBox);
                    },
                    () =>
                    {
                        ElementCache.Instance.Add(groupBox);
                        canvasController.UngroupShapes(groupBox, false);
                        canvasController.DeselectCurrentSelectedElements();
                        canvasController.SelectElements(selectedShapes);
                    });
            }
        }

        private void mnuUngroup_Click(object sender, EventArgs e)
        {
            // At this point, we can only ungroup one group.
            if (canvasController.SelectedElements.Count == 1)
            {
                FlowSharpLib.GroupBox groupBox = canvasController.SelectedElements[0] as FlowSharpLib.GroupBox;

                if (groupBox != null)
                {
                    List<GraphicElement> groupedShapes = new List<GraphicElement>(groupBox.GroupChildren);

                    canvasController.UndoStack.UndoRedo("Ungroup",
                    () =>
                    {
                        ElementCache.Instance.Add(groupBox);
                        canvasController.UngroupShapes(groupBox, false);
                        canvasController.DeselectCurrentSelectedElements();
                        canvasController.SelectElements(groupedShapes);
                    },
                    () =>
                    {
                        ElementCache.Instance.Remove(groupBox);
                        canvasController.GroupShapes(groupBox);
                        canvasController.DeselectCurrentSelectedElements();
                        canvasController.SelectElement(groupBox);
                    });
                }
            }
        }

        private void mnuPlugins_Click(object sender, EventArgs e)
        {
            new DlgPlugins().ShowDialog();
            pluginManager.UpdatePlugins();
        }

        private void mnuUndo_Click(object sender, EventArgs e)
        {
            Undo();
        }

        private void mnuRedo_Click(object sender, EventArgs e)
        {
            Redo();
        }

        /// <summary>
        /// Return true if operation should be cancelled.
        /// </summary>
        protected bool CheckForChanges()
        {
            bool ret = false;

            if (canvasController.UndoStack.HasChanges)
            {
                DialogResult res = MessageBox.Show("Do you wish to save changes to this drawing?", "Save Changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                ret = res == DialogResult.Cancel;

                if (res == DialogResult.Yes)
                {
                    ret = !SaveOrSaveAs();   // override because of possible cancel in save operation.
                }
                else
                {
                    canvasController.UndoStack.ClearStacks();       // Prevents second "are you sure" when exiting with Ctrl+X
                }
            }

            return ret;
        }

        protected bool SaveOrSaveAs(bool forceSaveAs = false)
        {
            bool ret = true;

            if (String.IsNullOrEmpty(filename) || forceSaveAs)
            {
                ret = SaveAs();
            }
            else
            {
                string data = Persist.Serialize(canvasController.Elements);
                File.WriteAllText(filename, data);
            }

            return ret;
        }

        protected bool SaveAs()
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "FlowSharp (*.fsd)|*.fsd|PNG (*.png)|*.png";
            DialogResult res = sfd.ShowDialog();
            string ext = ".fsd";

            if (res == DialogResult.OK)
            {
                ext = Path.GetExtension(sfd.FileName).ToLower();

                if (ext == ".png")
                {
                    canvasController.SaveAsPng(sfd.FileName);
                }
                else
                {
                    filename = sfd.FileName;
                    string data = Persist.Serialize(canvasController.Elements);
                    File.WriteAllText(filename, data);
                    UpdateCaption();
                }
            }

            return res == DialogResult.OK && ext != ".png";
        }
    }
}
