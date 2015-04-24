﻿//Ohana3DS Dock Control made by gdkchan
//
//TODO:
//- Some functions can be a bit optimized (+ reduce redundant code)

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using System.Drawing;

namespace Ohana3DS_Rebirth.GUI
{
    public partial class ODock : Control
    {
        const int minimumWidth = 128;
        const int minimumHeight = 64;

        const int defaultSideSize = 192;

        int rightDockWidth = defaultSideSize;
        int leftDockWidth = defaultSideSize;
        int bottomDockHeight = defaultSideSize;
        int topDockHeight = defaultSideSize;

        float rightDockProportionW;
        float leftDockProportionW;
        float bottomDockProportionH;
        float topDockProportionH;

        const int gripSize = 4;

        public enum dockMode
        {
            Floating,
            Left,
            Right,
            Top,
            Bottom,
            Center
        }
        public enum resizeMode
        {
            formOnly,
            entireSide
        }
        private class formInfoStruct
        {
            public ODockWindow form;
            public Size originalSize;
            public SizeF formProportions;
            public int index;
            public dockMode dock;
            public bool hasGrip;
            public bool denyDock;
        }
        List<formInfoStruct> formInfo = new List<formInfoStruct>();
        private int formIndex;

        private bool drag;
        private int dragIndex;
        private Point dragMousePosition;
        private resizeMode dragMode;
        private dockMode dragSide;

        public ODock()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            this.Resize += new EventHandler(Control_Resize);
            this.MouseMove += new MouseEventHandler(Control_MouseMove);
            this.MouseDown += new MouseEventHandler(Control_MouseDown);
            this.MouseUp += new MouseEventHandler(Control_MouseUp);
            InitializeComponent();
        }

        /// <summary>
        ///     Add a Form on the Dock Container.
        ///     Please note that the Tag property of the Form will contains the indentification Index, so do not change!
        /// </summary>
        /// <param name="window">The Form (ODockWindow control)</param>
        public void launch(ODockWindow window)
        {
            this.Controls.Add(window);
            window.Move += new EventHandler(Form_Move);
            window.MoveEnded += new EventHandler(Form_MoveEnded);
            window.VisibleChanged += new EventHandler(Form_VisibleChanged);

            formInfoStruct info = new formInfoStruct();
            info.index = formIndex;
            info.dock = dockMode.Floating;
            info.form = window;
            info.originalSize = window.Size;
            formInfo.Add(info);
            window.Tag = formIndex;
            window.BringToFront();
            formIndex += 1;
        }

        private void Control_Resize(Object sender, EventArgs e)
        {
            if (!hasDockedForm()) return;
            Rectangle rect = new Rectangle(0, 0, this.Width, this.Height);

            rightDockWidth = (int)(rect.Width * rightDockProportionW);
            leftDockWidth = (int)(rect.Width * leftDockProportionW);
            bottomDockHeight = (int)(rect.Height * bottomDockProportionH);
            topDockHeight = (int)(rect.Height * topDockProportionH);

            resize(rect, dockMode.Right);
            resize(rect, dockMode.Left);
            resize(rect, dockMode.Bottom);
            resize(rect, dockMode.Top);
            resize(rect, dockMode.Center);
        }

        private void resize(Rectangle rect, dockMode dock)
        {
            Rectangle dockRect = getDockRect(dock);

            int top = dockRect.Top;
            int left = dockRect.Left;

            List<int> indexList = new List<int>();

            switch (dock)
            {
                case dockMode.Left:
                case dockMode.Right:
                    int indexRL = getTopmostForm(dock);
                    while (indexRL > -1)
                    {
                        indexList.Add(indexRL);
                        indexRL = getBelowForm(getRectangle(formInfo[indexRL].form), dock, indexRL);
                    }

                    for (int i = 0; i < indexList.Count; i++)
                    {
                        int index = indexList[i];
                        formInfo[index].form.Top = top;
                        formInfo[index].form.Left = dockRect.Left;
                        formInfo[index].form.Width = dockRect.Width;
                        if (i != indexList.Count - 1)
                        {
                            formInfo[index].form.Height = (int)(rect.Height * formInfo[index].formProportions.Height) - gripSize;
                            formInfo[index].hasGrip = true;
                        }
                        else
                        {
                            formInfo[index].form.Height = ((dockRect.Y + dockRect.Height) - formInfo[index].form.Top);
                            formInfo[index].hasGrip = false;
                        }
                        top += formInfo[index].form.Height + gripSize;
                    }

                    break;

                case dockMode.Bottom:
                case dockMode.Top:
                    int indexBT = getLeftmostForm(dock);
                    while (indexBT > -1)
                    {
                        indexList.Add(indexBT);
                        indexBT = getRightForm(getRectangle(formInfo[indexBT].form), dock, indexBT);
                    }

                    for (int i = 0; i < indexList.Count; i++)
                    {
                        int index = indexList[i];
                        formInfo[index].form.Top = dockRect.Top;
                        formInfo[index].form.Left = left;
                        formInfo[index].form.Height = dockRect.Height;
                        if (i != indexList.Count - 1)
                        {
                            formInfo[index].form.Width = (int)(rect.Width * formInfo[index].formProportions.Width) - gripSize;
                            formInfo[index].hasGrip = true;
                        }
                        else
                        {
                            formInfo[index].form.Width = ((dockRect.X + dockRect.Width) - formInfo[index].form.Left);
                            formInfo[index].hasGrip = false;
                        }
                        left += formInfo[index].form.Width + gripSize;
                    }

                    break;

                case dockMode.Center:
                    int centerIndex = getCenterForm();
                    if (centerIndex > -1)
                    {
                        formInfo[centerIndex].form.Location = dockRect.Location;
                        formInfo[centerIndex].form.Size = dockRect.Size;
                    }

                    break;
            }
        }

        private void Control_MouseMove(Object sender, MouseEventArgs e)
        {
            if (drag)
            {
                Rectangle rect = new Rectangle(0, 0, this.Width, this.Height);

                if (dragMode == resizeMode.entireSide)
                {
                    switch (dragSide)
                    {
                        case dockMode.Right:
                            bool canResizeRight = false;
                            int newWidthRight = (rect.X + rect.Width) - this.PointToClient(Cursor.Position).X;
                            if (newWidthRight < minimumWidth) newWidthRight = minimumWidth;
                            if (newWidthRight > this.Width - gripSize) newWidthRight = this.Width - gripSize;
                            int differenceRight = newWidthRight - rightDockWidth;
                            if (differenceRight > 0) //Aumentou
                            {
                                while (newWidthRight > rightDockWidth)
                                {
                                    canResizeRight = getSpaceToCreateDockSide(dockMode.Right, differenceRight - gripSize);
                                    if (canResizeRight) break;
                                    newWidthRight--;
                                    differenceRight = newWidthRight - rightDockWidth;
                                }
                            }
                            else if (differenceRight < 0) //Diminuiu
                            {
                                canResizeRight = true;
                                int diff = differenceRight * -1;

                                if (formCount(dockMode.Bottom) > 0)
                                {
                                    int bottomFormIndex = getRightmostForm(dockMode.Bottom);
                                    formInfo[bottomFormIndex].form.Width += diff;
                                }

                                if (formCount(dockMode.Center) > 0)
                                {
                                    int centerFormIndex = getCenterForm();
                                    formInfo[centerFormIndex].form.Width += diff;
                                }

                                if (formCount(dockMode.Top) > 0)
                                {
                                    int topFormIndex = getRightmostForm(dockMode.Top);
                                    formInfo[topFormIndex].form.Width += diff;
                                }
                            }

                            if (canResizeRight)
                            {
                                rightDockWidth = newWidthRight;

                                for (int i = 0; i < formInfo.Count; i++)
                                {
                                    if (formInfo[i].dock == dragSide)
                                    {
                                        formInfo[i].form.SuspendLayout();
                                        formInfo[i].form.Width = rightDockWidth;
                                        formInfo[i].form.Left = (rect.X + rect.Width) - rightDockWidth;
                                    }
                                }

                                for (int i = 0; i < formInfo.Count; i++)
                                {
                                    if (formInfo[i].dock == dragSide)
                                    {
                                        formInfo[i].form.ResumeLayout();
                                    }
                                }
                            }

                            break;

                        case dockMode.Left:
                            bool canResizeLeft = false;
                            int newWidthLeft = this.PointToClient(Cursor.Position).X - rect.X;
                            if (newWidthLeft < minimumWidth) newWidthLeft = minimumWidth;
                            if (newWidthLeft > this.Width - gripSize) newWidthLeft = this.Width - gripSize;
                            int differenceLeft = newWidthLeft - leftDockWidth;
                            if (differenceLeft > 0) //Aumentou
                            {
                                while (newWidthLeft > leftDockWidth)
                                {
                                    canResizeLeft = getSpaceToCreateDockSide(dockMode.Left, differenceLeft - gripSize);
                                    if (canResizeLeft) break;
                                    newWidthLeft--;
                                    differenceLeft = newWidthLeft - leftDockWidth;
                                }
                            }
                            else if (differenceLeft < 0) //Diminuiu
                            {
                                canResizeLeft = true;
                                int diff = differenceLeft * -1;

                                if (formCount(dockMode.Bottom) > 0)
                                {
                                    int bottomFormIndex = getLeftmostForm(dockMode.Bottom);
                                    formInfo[bottomFormIndex].form.Left -= diff;
                                    formInfo[bottomFormIndex].form.Width += diff;
                                }

                                if (formCount(dockMode.Center) > 0)
                                {
                                    int centerFormIndex = getCenterForm();
                                    formInfo[centerFormIndex].form.Left -= diff;
                                    formInfo[centerFormIndex].form.Width += diff;
                                }

                                if (formCount(dockMode.Top) > 0)
                                {
                                    int topFormIndex = getLeftmostForm(dockMode.Top);
                                    formInfo[topFormIndex].form.Left -= diff;
                                    formInfo[topFormIndex].form.Width += diff;
                                }
                            }

                            if (canResizeLeft)
                            {
                                leftDockWidth = newWidthLeft;

                                for (int i = 0; i < formInfo.Count; i++)
                                {
                                    if (formInfo[i].dock == dragSide)
                                    {
                                        formInfo[i].form.SuspendLayout();
                                        formInfo[i].form.Width = leftDockWidth;
                                    }
                                }

                                for (int i = 0; i < formInfo.Count; i++)
                                {
                                    if (formInfo[i].dock == dragSide)
                                    {
                                        formInfo[i].form.ResumeLayout();
                                    }
                                }
                            }
                            
                            break;

                        case dockMode.Bottom:
                            bool canResizeBottom = false;
                            int newBottomHeight = (rect.Y + rect.Height) - this.PointToClient(Cursor.Position).Y;
                            if (newBottomHeight < minimumHeight) newBottomHeight = minimumHeight;
                            if (newBottomHeight > this.Height - gripSize) newBottomHeight = this.Height - gripSize;
                            int differenceBottom = newBottomHeight - bottomDockHeight;
                            if (differenceBottom > 0) //Aumentou
                            {
                                while (newBottomHeight > bottomDockHeight)
                                {
                                    canResizeBottom = getSpaceToCreateDockSide(dockMode.Bottom, differenceBottom - gripSize);
                                    if (canResizeBottom) break;
                                    newBottomHeight--;
                                    differenceBottom = newBottomHeight - bottomDockHeight;
                                }
                            }
                            else if (differenceBottom < 0) //Diminuiu
                            {
                                canResizeBottom = true;
                                int diff = differenceBottom * -1;

                                if (formCount(dockMode.Center) > 0)
                                {
                                    int centerFormIndex = getCenterForm();
                                    formInfo[centerFormIndex].form.Height += diff;
                                }
                            }

                            if (canResizeBottom)
                            {
                                bottomDockHeight = newBottomHeight;

                                for (int i = 0; i < formInfo.Count; i++)
                                {
                                    if (formInfo[i].dock == dragSide)
                                    {
                                        formInfo[i].form.SuspendLayout();
                                        formInfo[i].form.Height = bottomDockHeight;
                                        formInfo[i].form.Top = (rect.Y + rect.Height) - bottomDockHeight;
                                    }
                                }

                                for (int i = 0; i < formInfo.Count; i++)
                                {
                                    if (formInfo[i].dock == dragSide)
                                    {
                                        formInfo[i].form.ResumeLayout();
                                    }
                                }
                            }

                            break;

                        case dockMode.Top:
                            bool canResizeTop = false;
                            int newTopHeight = this.PointToClient(Cursor.Position).Y - rect.Y;
                            if (newTopHeight < minimumHeight) newTopHeight = minimumHeight;
                            if (newTopHeight > this.Height - gripSize) newTopHeight = this.Height - gripSize;
                            int differenceTop = newTopHeight - topDockHeight;
                            if (differenceTop > 0) //Aumentou
                            {
                                while (newTopHeight > topDockHeight)
                                {
                                    canResizeTop = getSpaceToCreateDockSide(dockMode.Top, differenceTop - gripSize);
                                    if (canResizeTop) break;
                                    newTopHeight--;
                                    differenceTop = newTopHeight - topDockHeight;
                                }
                            }
                            else if (differenceTop < 0) //Diminuiu
                            {
                                canResizeTop = true;
                                int diff = differenceTop * -1;

                                if (formCount(dockMode.Center) > 0)
                                {
                                    int centerFormIndex = getCenterForm();
                                    formInfo[centerFormIndex].form.Top -= diff;
                                    formInfo[centerFormIndex].form.Height += diff;
                                }
                            }

                            if (canResizeTop)
                            {
                                topDockHeight = newTopHeight;

                                for (int i = 0; i < formInfo.Count; i++)
                                {
                                    if (formInfo[i].dock == dragSide)
                                    {
                                        formInfo[i].form.SuspendLayout();
                                        formInfo[i].form.Height = topDockHeight;
                                    }
                                }

                                for (int i = 0; i < formInfo.Count; i++)
                                {
                                    if (formInfo[i].dock == dragSide)
                                    {
                                        formInfo[i].form.ResumeLayout();
                                    }
                                }
                            }

                            break;
                    }
                }
                else if (dragMode == resizeMode.formOnly)
                {
                    rect = getDockRect(formInfo[dragIndex].dock);

                    switch (formInfo[dragIndex].dock)
                    {
                        case dockMode.Right:
                        case dockMode.Left:
                            int dragDistanceY = Cursor.Position.Y - dragMousePosition.Y;

                            int belowForm = getBelowForm(getRectangle(formInfo[dragIndex].form), formInfo[dragIndex].dock, dragIndex);
                            if (belowForm == -1) return;
                            int belowFormHeight, belowFormHeightMinimum;
                            int belowBelowForm = getBelowForm(getRectangle(formInfo[belowForm].form), formInfo[dragIndex].dock, belowForm);
                            if (formInfo[belowForm].hasGrip)
                            {
                                if (belowBelowForm == -1) return;
                                belowFormHeight = (formInfo[belowBelowForm].form.Top - (formInfo[belowForm].form.Top + dragDistanceY)) - gripSize;
                                belowFormHeightMinimum = formInfo[belowForm].form.Height = (formInfo[belowBelowForm].form.Top - formInfo[belowForm].form.Top) - gripSize;
                            }
                            else
                            {
                                belowFormHeight = (rect.Top + rect.Height) - (formInfo[belowForm].form.Top + dragDistanceY);
                                belowFormHeightMinimum = formInfo[belowForm].form.Height = (rect.Top + rect.Height) - formInfo[belowForm].form.Top;
                            }

                            if ((formInfo[dragIndex].form.Height + dragDistanceY >= minimumHeight) && (belowFormHeight >= minimumHeight || (formInfo[belowForm].form.Height < minimumHeight && belowFormHeight >= formInfo[belowForm].form.Height)))
                            {
                                formInfo[dragIndex].form.Height += dragDistanceY;
                                formInfo[belowForm].form.Top += dragDistanceY;
                                formInfo[belowForm].form.Height = belowFormHeight;
                            }
                            else if (formInfo[dragIndex].form.Height + dragDistanceY < minimumHeight && belowFormHeightMinimum > minimumHeight)
                            {
                                formInfo[dragIndex].form.Height = minimumHeight;
                                formInfo[belowForm].form.Top = (formInfo[dragIndex].form.Top + formInfo[dragIndex].form.Height) + gripSize;
                                formInfo[belowForm].form.Height = belowFormHeightMinimum;
                            }
                            else if (belowFormHeight < minimumHeight)
                            {
                                int top;
                                
                                if (formInfo[belowForm].hasGrip)
                                {
                                    top = (formInfo[belowBelowForm].form.Top - formInfo[belowForm].form.Height) - gripSize;
                                }
                                else
                                {
                                    top = (rect.Top + rect.Height) - formInfo[belowForm].form.Height;
                                }

                                int height = (formInfo[belowForm].form.Top - formInfo[dragIndex].form.Top) - gripSize;
                                if (height < minimumHeight) return;
                                formInfo[belowForm].form.Top = top;
                                formInfo[belowForm].form.Height = minimumHeight;
                                formInfo[dragIndex].form.Height = height;
                            }

                            break;

                        case dockMode.Bottom:
                        case dockMode.Top:
                            int dragDistanceX = Cursor.Position.X - dragMousePosition.X;

                            int rightForm = getRightForm(getRectangle(formInfo[dragIndex].form), formInfo[dragIndex].dock, dragIndex);
                            if (rightForm == -1) return;
                            int rightFormWidth, rightFormWidthMinimum;
                            int rightRightForm = getRightForm(getRectangle(formInfo[rightForm].form), formInfo[dragIndex].dock, rightForm);
                            if (formInfo[rightForm].hasGrip)
                            {
                                if (rightRightForm == -1) return;
                                rightFormWidth = (formInfo[rightRightForm].form.Left - (formInfo[rightForm].form.Left + dragDistanceX)) - gripSize;
                                rightFormWidthMinimum = (formInfo[rightRightForm].form.Left - formInfo[rightForm].form.Left) - gripSize;
                            }
                            else
                            {
                                rightFormWidth = (rect.Left + rect.Width) - (formInfo[rightForm].form.Left + dragDistanceX);
                                rightFormWidthMinimum = (rect.Left + rect.Width) - formInfo[rightForm].form.Left;
                            }

                            if (formInfo[dragIndex].form.Width + dragDistanceX >= minimumWidth && (rightFormWidth >= minimumWidth || (formInfo[rightForm].form.Width < minimumWidth && rightFormWidth >= formInfo[rightForm].form.Width)))
                            {
                                formInfo[dragIndex].form.Width += dragDistanceX;
                                formInfo[rightForm].form.Left += dragDistanceX;
                                formInfo[rightForm].form.Width = rightFormWidth;
                                
                            }
                            else if (formInfo[dragIndex].form.Width + dragDistanceX < minimumWidth && rightFormWidthMinimum > minimumWidth)
                            {
                                formInfo[dragIndex].form.Width = minimumWidth;
                                formInfo[rightForm].form.Left = (formInfo[dragIndex].form.Left + formInfo[dragIndex].form.Width) + gripSize;
                                formInfo[rightForm].form.Width = rightFormWidthMinimum;
                            }
                            else if (rightFormWidth < minimumWidth)
                            {
                                int left;
                                
                                if (formInfo[rightForm].hasGrip)
                                {
                                    left = (formInfo[rightRightForm].form.Left - formInfo[rightForm].form.Width) - gripSize;
                                }
                                else
                                {
                                    left = (rect.Left + rect.Width) - formInfo[rightForm].form.Width;
                                }

                                int width = (formInfo[rightForm].form.Left - formInfo[dragIndex].form.Left) - gripSize;
                                if (width < minimumWidth) return;
                                formInfo[rightForm].form.Left = left;
                                formInfo[rightForm].form.Width = minimumWidth;
                                formInfo[dragIndex].form.Width = width;
                            }

                            break;
                    }
                }

                dragMousePosition = Cursor.Position;
                calculateProportions();
            }
            else
            {
                Rectangle rect = new Rectangle(0, 0, this.Width, this.Height);
                Rectangle mouseRect = new Rectangle(this.PointToClient(Cursor.Position), new Size(1, 1));

                for (int i = 0; i < formInfo.Count; i++)
                {
                    if (formInfo[i].hasGrip)
                    {
                        switch (formInfo[i].dock)
                        {
                            case dockMode.Right:
                            case dockMode.Left:
                                Rectangle gripRectRL = new Rectangle(formInfo[i].form.Left, formInfo[i].form.Top + formInfo[i].form.Height, formInfo[i].form.Width, gripSize);
                                if (mouseRect.IntersectsWith(gripRectRL)) Cursor.Current = Cursors.HSplit;
                                break;

                            case dockMode.Bottom:
                            case dockMode.Top:
                                Rectangle gripRectBT = new Rectangle(formInfo[i].form.Left + formInfo[i].form.Width, formInfo[i].form.Top, gripSize, formInfo[i].form.Height);
                                if (mouseRect.IntersectsWith(gripRectBT)) Cursor.Current = Cursors.VSplit;
                                break;
                        }
                    }
                }

                Rectangle rightDockDrag = new Rectangle(((rect.X + rect.Width) - rightDockWidth) - gripSize, rect.Y, gripSize, rect.Height);
                Rectangle leftDockDrag = new Rectangle(rect.X +  leftDockWidth, rect.Y, gripSize, rect.Height);
                Rectangle bottomDockDrag = new Rectangle(rect.X, ((rect.Y + rect.Height) - bottomDockHeight) - gripSize, rect.Width, gripSize);
                Rectangle topDockDrag = new Rectangle(rect.X, rect.Y + topDockHeight, rect.Width, gripSize);

                if ((formCount(dockMode.Right) > 0) && mouseRect.IntersectsWith(rightDockDrag)) Cursor.Current = Cursors.VSplit;
                if ((formCount(dockMode.Left) > 0) && mouseRect.IntersectsWith(leftDockDrag)) Cursor.Current = Cursors.VSplit;
                if ((formCount(dockMode.Bottom) > 0) && mouseRect.IntersectsWith(bottomDockDrag)) Cursor.Current = Cursors.HSplit;
                if ((formCount(dockMode.Top) > 0) && mouseRect.IntersectsWith(topDockDrag)) Cursor.Current = Cursors.HSplit;
            }
        }

        private void Control_MouseDown(Object sender, MouseEventArgs e)
        {
            Rectangle rect = new Rectangle(0, 0, this.Width, this.Height);
            Rectangle mouseRect = new Rectangle(this.PointToClient(Cursor.Position), new Size(1, 1));

            for (int i = 0; i < formInfo.Count; i++)
            {
                if (formInfo[i].hasGrip)
                {
                    switch (formInfo[i].dock)
                    {
                        case dockMode.Right:
                        case dockMode.Left:
                            Rectangle gripRectRL = new Rectangle(formInfo[i].form.Left, formInfo[i].form.Top + formInfo[i].form.Height, formInfo[i].form.Width, gripSize);

                            if (mouseRect.IntersectsWith(gripRectRL))
                            {
                                Cursor.Current = Cursors.HSplit;
                                drag = true;
                                dragIndex = i;
                                dragMousePosition = Cursor.Position;
                                dragMode = resizeMode.formOnly;
                                return;
                            }
                            break;

                        case dockMode.Bottom:
                        case dockMode.Top:
                            Rectangle gripRectBT = new Rectangle(formInfo[i].form.Left + formInfo[i].form.Width, formInfo[i].form.Top, gripSize, formInfo[i].form.Height);

                            if (mouseRect.IntersectsWith(gripRectBT))
                            {
                                Cursor.Current = Cursors.VSplit;
                                drag = true;
                                dragIndex = i;
                                dragMousePosition = Cursor.Position;
                                dragMode = resizeMode.formOnly;
                                return;
                            }
                            break;
                    }
                }
            }

            Rectangle rightDockDrag = new Rectangle(((rect.X + rect.Width) - rightDockWidth) - gripSize, rect.Y, gripSize, rect.Height);
            Rectangle leftDockDrag = new Rectangle(rect.X + leftDockWidth, rect.Y, gripSize, rect.Height);
            Rectangle bottomDockDrag = new Rectangle(rect.X, ((rect.Y + rect.Height) - bottomDockHeight) - gripSize, rect.Width, gripSize);
            Rectangle topDockDrag = new Rectangle(rect.X, rect.Y + topDockHeight, rect.Width, gripSize);

            if ((formCount(dockMode.Right) > 0) && mouseRect.IntersectsWith(rightDockDrag))
            {
                Cursor.Current = Cursors.VSplit;
                drag = true;
                dragMousePosition = Cursor.Position;
                dragMode = resizeMode.entireSide;
                dragSide = dockMode.Right;
            }
            else if ((formCount(dockMode.Left) > 0) && mouseRect.IntersectsWith(leftDockDrag))
            {
                Cursor.Current = Cursors.VSplit;
                drag = true;
                dragMousePosition = Cursor.Position;
                dragMode = resizeMode.entireSide;
                dragSide = dockMode.Left;
            }
            else if ((formCount(dockMode.Bottom) > 0) && mouseRect.IntersectsWith(bottomDockDrag))
            {
                Cursor.Current = Cursors.HSplit;
                drag = true;
                dragMousePosition = Cursor.Position;
                dragMode = resizeMode.entireSide;
                dragSide = dockMode.Bottom;
            }
            else if ((formCount(dockMode.Top) > 0) && mouseRect.IntersectsWith(topDockDrag))
            {
                Cursor.Current = Cursors.HSplit;
                drag = true;
                dragMousePosition = Cursor.Position;
                dragMode = resizeMode.entireSide;
                dragSide = dockMode.Top;
            }
        }

        private void Control_MouseUp(Object sender, MouseEventArgs e)
        {
            if (drag) drag = false;
        }

        //
        //
        //

        private void Form_Move(Object sender, EventArgs e)
        {
            if (drag) return;

            ODockWindow form = (ODockWindow)sender;
            int infoIndex = getFormInfoIndex((int)form.Tag);

            if (!formInfo[infoIndex].form.Drag) return;

            Rectangle rect = new Rectangle(0, 0, this.Width, this.Height);
            Rectangle rightDock = getDockRect(dockMode.Right);
            Rectangle leftDock = getDockRect(dockMode.Left);
            Rectangle bottomDock = getDockRect(dockMode.Bottom);
            Rectangle topDock = getDockRect(dockMode.Top);
            Rectangle centerDock = getDockRect(dockMode.Center);

            Rectangle dockRect = new Rectangle(form.Location, form.Size);

            Rectangle formDockRect = new Rectangle();
            switch (formInfo[infoIndex].dock)
            {
                case dockMode.Right: formDockRect = rightDock; break;
                case dockMode.Left: formDockRect = leftDock; break;
                case dockMode.Bottom: formDockRect = bottomDock; break;
                case dockMode.Top: formDockRect = topDock; break;
                case dockMode.Center: formDockRect = centerDock; break;
            }

            if (!dockRect.IntersectsWith(formDockRect) || formInfo[infoIndex].denyDock)
            {
                dockMode oldDock = formInfo[infoIndex].dock;
                formInfo[infoIndex].dock = dockMode.Floating;
                formInfo[infoIndex].hasGrip = false;
                formInfo[infoIndex].denyDock = false;
                formInfo[infoIndex].form.Size = formInfo[infoIndex].originalSize;
                formInfo[infoIndex].form.BringToFront();
                autoArrange(oldDock, formDockRect);

                //Caso um Dock Side tenha sido removido...
                if (oldDock == dockMode.Right && formCount(dockMode.Right) == 0)
                {
                    int width = rightDockWidth + gripSize;
                    if (formCount(dockMode.Bottom) > 0)
                    {
                        int index = getRightmostForm(dockMode.Bottom);
                        formInfo[index].form.Width += width;
                    }

                    if (formCount(dockMode.Top) > 0)
                    {
                        int index = getRightmostForm(dockMode.Top);
                        formInfo[index].form.Width += width;
                    }
                }
                else if (oldDock == dockMode.Left && formCount(dockMode.Left) == 0)
                {
                    int width = leftDockWidth + gripSize;
                    if (formCount(dockMode.Bottom) > 0)
                    {
                        int index = getLeftmostForm(dockMode.Bottom);
                        formInfo[index].form.Left -= width;
                        formInfo[index].form.Width += width;
                    }

                    if (formCount(dockMode.Top) > 0)
                    {
                        int index = getLeftmostForm(dockMode.Top);
                        formInfo[index].form.Left -= width;
                        formInfo[index].form.Width += width;
                    }
                }

                if (formCount(dockMode.Center) > 0)
                {
                    int centerFormIndex = 0;
                    centerFormIndex = getCenterForm();
                    formInfo[centerFormIndex].form.Location = centerDock.Location;
                    formInfo[centerFormIndex].form.Size = centerDock.Size;
                }

                calculateProportions();
            }
        }

        private void Form_MoveEnded(Object sender, EventArgs e)
        {
            Control form = (Control)sender;
            int infoIndex = getFormInfoIndex((int)form.Tag);

            Rectangle rect = new Rectangle(0, 0, this.Width, this.Height);

            if (formCount(dockMode.Right) == 0) rightDockWidth = defaultSideSize; //Reseta valores caso não tenha nada no Dock Side ainda
            if (formCount(dockMode.Left) == 0) leftDockWidth = defaultSideSize;
            if (formCount(dockMode.Bottom) == 0) bottomDockHeight = defaultSideSize;
            if (formCount(dockMode.Top) == 0) topDockHeight = defaultSideSize;
            
            Rectangle rightDock = getDockRect(dockMode.Right);
            Rectangle leftDock = getDockRect(dockMode.Left);
            Rectangle bottomDock = getDockRect(dockMode.Bottom);
            Rectangle topDock = getDockRect(dockMode.Top);
            Rectangle centerDock = getDockRect(dockMode.Center);

            //Código que verifica se um Dock deve ocorrer, e posiciona caso isso ocorra
            Rectangle dockRect = new Rectangle(form.Location, form.Size);

            if (formCount(dockMode.Center) == 0 && dockRect.IntersectsWith(centerDock))
            {
                if (centerDock.Width >= minimumWidth && centerDock.Height >= minimumHeight)
                {
                    form.Location = centerDock.Location;
                    form.Size = centerDock.Size;
                    form.SendToBack();

                    formInfo[infoIndex].hasGrip = false;
                    formInfo[infoIndex].dock = dockMode.Center;
                }
                else
                {
                    formInfo[infoIndex].denyDock = true;
                }
            }
            else if (dockRect.IntersectsWith(rightDock) || dockRect.IntersectsWith(leftDock)) //Lados esquerdo e direito
            {
                dockMode currentDock;
                Rectangle currentRect;

                bool deny = false;

                if (dockRect.IntersectsWith(rightDock))
                {
                    currentDock = dockMode.Right;
                    currentRect = rightDock;
                }
                else
                {
                    currentDock = dockMode.Left;
                    currentRect = leftDock;
                }

                if (formCount(currentDock) == 0)
                {
                    if (!getSpaceToCreateDockSide(currentDock, currentDock == dockMode.Right ? rightDockWidth : leftDockWidth))
                    {
                        formInfo[infoIndex].denyDock = true;
                        deny = true;
                        form.BringToFront();
                    }
                }

                if (!deny)
                {
                    form.SuspendLayout();

                    formInfo[infoIndex].dock = dockMode.Floating;
                    formInfo[infoIndex].form.Size = formInfo[infoIndex].originalSize;
                    autoArrange(currentDock, currentRect);

                    int aboveForm = getAboveForm(dockRect, currentDock, infoIndex);
                    int belowForm = getBelowForm(dockRect, currentDock, infoIndex);

                    if (aboveForm > -1 && formInfo[aboveForm].form.Height / 2 > minimumHeight)
                    {
                        if (formInfo[aboveForm].hasGrip)
                        {
                            formInfo[aboveForm].form.Height = ((formInfo[aboveForm].form.Height + gripSize) / 2) - gripSize;
                        }
                        else
                        {
                            formInfo[aboveForm].form.Height = (formInfo[aboveForm].form.Height / 2) - gripSize;
                            formInfo[aboveForm].hasGrip = true;
                        }
                        int top = (formInfo[aboveForm].form.Top - rect.Top) + (formInfo[aboveForm].form.Height + gripSize);

                        form.Location = new Point(currentRect.X, rect.Top + top);
                        int height = currentRect.Height - top;
                        if (belowForm > -1)
                        {
                            height = ((formInfo[belowForm].form.Top - rect.Top) - top) - gripSize;
                            formInfo[infoIndex].hasGrip = true;
                        }
                        else
                        {
                            formInfo[infoIndex].hasGrip = false;
                        }

                        form.Size = new Size(currentRect.Width, height);
                        form.SendToBack();

                        formInfo[infoIndex].dock = currentDock;
                    }
                    else if (belowForm > -1 && formInfo[belowForm].form.Height / 2 > minimumHeight)
                    {
                        int height;
                        if (formInfo[belowForm].hasGrip)
                        {
                            int belowBelowForm = getBelowForm(getRectangle(formInfo[belowForm].form), currentDock, getFormInfoIndex((int)formInfo[belowForm].form.Tag));
                            height = (formInfo[belowForm].form.Height + gripSize) / 2;
                            formInfo[belowForm].form.Height = (formInfo[belowBelowForm].form.Top - (formInfo[belowForm].form.Top + height)) - gripSize;
                        }
                        else
                        {
                            height = formInfo[belowForm].form.Height / 2;
                            formInfo[belowForm].form.Height = (rect.Top + rect.Height) - (formInfo[belowForm].form.Top + height);
                        }

                        int top = formInfo[belowForm].form.Top;
                        formInfo[belowForm].form.Top += height;

                        form.Location = new Point(currentRect.X, top);
                        form.Size = new Size(currentRect.Width, height - gripSize);
                        form.SendToBack();

                        formInfo[infoIndex].hasGrip = true;
                        formInfo[infoIndex].dock = currentDock;
                    }
                    else if (aboveForm == -1 && belowForm == -1)
                    {
                        form.Location = currentRect.Location;
                        form.Size = currentRect.Size;
                        form.SendToBack();

                        formInfo[infoIndex].hasGrip = false;
                        formInfo[infoIndex].dock = currentDock;
                    }
                    else
                    {
                        formInfo[infoIndex].denyDock = true;
                        form.BringToFront();
                    }

                    form.ResumeLayout();
                }
            }
            else if (dockRect.IntersectsWith(bottomDock) || dockRect.IntersectsWith(topDock))
            {
                dockMode currentDock;
                Rectangle currentRect;

                bool deny = false;

                if (dockRect.IntersectsWith(bottomDock))
                {
                    currentDock = dockMode.Bottom;
                    currentRect = bottomDock;
                }
                else
                {
                    currentDock = dockMode.Top;
                    currentRect = topDock;
                }

                if (formCount(currentDock) == 0)
                {
                    if (!getSpaceToCreateDockSide(currentDock, currentDock == dockMode.Bottom ? bottomDockHeight : topDockHeight))
                    {
                        formInfo[infoIndex].denyDock = true;
                        deny = true;
                        form.BringToFront();
                    }
                }

                if (!deny)
                {
                    form.SuspendLayout();

                    formInfo[infoIndex].dock = dockMode.Floating;
                    formInfo[infoIndex].form.Size = formInfo[infoIndex].originalSize;
                    autoArrange(currentDock, currentRect);

                    int leftForm = getLeftForm(dockRect, currentDock, infoIndex);
                    int rightForm = getRightForm(dockRect, currentDock, infoIndex);

                    if (leftForm > -1 && formInfo[leftForm].form.Width / 2 > minimumWidth)
                    {
                        if (formInfo[leftForm].hasGrip)
                        {
                            formInfo[leftForm].form.Width = ((formInfo[leftForm].form.Width + gripSize) / 2) - gripSize;
                        }
                        else
                        {
                            formInfo[leftForm].form.Width = (formInfo[leftForm].form.Width / 2) - gripSize;
                            formInfo[leftForm].hasGrip = true;
                        }
                        int left = (formInfo[leftForm].form.Left - rect.Left) + (formInfo[leftForm].form.Width + gripSize);

                        form.Location = new Point(rect.Left + left, currentRect.Y);
                        int width = currentRect.Width - left;
                        if (rightForm > -1)
                        {
                            width = ((formInfo[rightForm].form.Left - rect.Left) - left) - gripSize;
                            formInfo[infoIndex].hasGrip = true;
                        }
                        else
                        {
                            formInfo[infoIndex].hasGrip = false;
                        }

                        form.Size = new Size(width, currentRect.Height);
                        form.SendToBack();

                        formInfo[infoIndex].dock = currentDock;
                    }
                    else if (rightForm > -1 && formInfo[rightForm].form.Width / 2 > minimumWidth)
                    {
                        int width;
                        if (formInfo[rightForm].hasGrip)
                        {
                            int rightRightForm = getRightForm(getRectangle(formInfo[rightForm].form), currentDock, getFormInfoIndex((int)formInfo[rightForm].form.Tag));
                            width = (formInfo[rightForm].form.Width + gripSize) / 2;
                            formInfo[rightForm].form.Width = (formInfo[rightRightForm].form.Left - (formInfo[rightForm].form.Left + width)) - gripSize;
                        }
                        else
                        {
                            width = formInfo[rightForm].form.Width / 2;
                            formInfo[rightForm].form.Width = (rect.Left + rect.Width) - (formInfo[rightForm].form.Left + width);
                        }

                        int left = formInfo[rightForm].form.Left;
                        formInfo[rightForm].form.Left += width;

                        form.Location = new Point(left, currentRect.Y);
                        form.Size = new Size(width - gripSize, currentRect.Height);
                        form.SendToBack();

                        formInfo[infoIndex].hasGrip = true;
                        formInfo[infoIndex].dock = currentDock;
                    }
                    else if (leftForm == -1 && rightForm == -1)
                    {
                        form.Location = currentRect.Location;
                        form.Size = currentRect.Size;
                        form.SendToBack();

                        formInfo[infoIndex].hasGrip = false;
                        formInfo[infoIndex].dock = currentDock;
                    }
                    else
                    {
                        formInfo[infoIndex].denyDock = true;
                        form.BringToFront();
                    }

                    form.ResumeLayout();
                }
            }
            else
            {
                formInfo[infoIndex].denyDock = true;
            }

            autoArrange(dockMode.Right, getDockRect(dockMode.Right)); //Sim, é necessário obter os valores again...
            autoArrange(dockMode.Left, getDockRect(dockMode.Left));
            autoArrange(dockMode.Bottom, getDockRect(dockMode.Bottom));
            autoArrange(dockMode.Top, getDockRect(dockMode.Top));
            if (formCount(dockMode.Center) > 0)
            {
                int centerFormIndex = getCenterForm();
                centerDock = getDockRect(dockMode.Center);
                formInfo[centerFormIndex].form.Location = centerDock.Location;
                formInfo[centerFormIndex].form.Size = centerDock.Size;
            }

            calculateProportions(true);
        }

        private void Form_VisibleChanged(Object sender, EventArgs e)
        {
            Control form = (Control)sender;
            int infoIndex = getFormInfoIndex((int)form.Tag);

            if (formInfo[infoIndex].dock != dockMode.Floating)
            {
                dockMode oldDock = formInfo[infoIndex].dock;
                formInfo[infoIndex].dock = dockMode.Floating;
                autoArrange(oldDock, getDockRect(oldDock));
                formInfo[infoIndex].form.Size = formInfo[infoIndex].originalSize;
                formInfo[infoIndex].form.Location = Point.Empty;

                calculateProportions();
            }
        }

        public void remove(int tagIndex)
        {
            int infoIndex = getFormInfoIndex(tagIndex);

            dockMode dock = formInfo[infoIndex].dock;
            if (dock != dockMode.Floating) autoArrange(dock, getDockRect(dock));
            formInfo[infoIndex].form.Dispose();
            formInfo.RemoveAt(infoIndex);
        }

        /// <summary>
        ///     Get the Form placed exactly above the Rectangle passed to the function.
        ///     Returns -1 if it can't find any Form above.
        /// </summary>
        /// <param name="rect">The rectangle at where your form is located</param>
        /// <param name="dock">The docking mode that will be used</param>
        /// <param name="ignoreIndex">Index of the Form used to be ignored</param>
        /// <returns>Returns the Index for the Form on the list, or -1 if it can't be found</returns>
        private int getAboveForm(Rectangle rect, dockMode dock, int ignoreIndex)
        {
            int index = -1;
            int top = 0;

            for (int i = 0; i < formInfo.Count; i++)
            {
                if (formInfo[i].dock == dock && formInfo[i].form.Top < rect.Y && i != ignoreIndex)
                {
                    if (formInfo[i].form.Top >= top)
                    {
                        top = formInfo[i].form.Top;
                        index = i;
                    }
                }
            }

            return index;
        }

        /// <summary>
        ///     Get the Form placed exactly below the Rectangle passed to the function.
        ///     Returns -1 if it can't find any Form below.
        /// </summary>
        /// <param name="rect">The rectangle at where your form is located</param>
        /// <param name="dock">The docking mode that will be used</param>
        /// <param name="ignoreIndex">Index of the Form used to be ignored</param>
        /// <returns>Returns the Index for the Form on the list, or -1 if it can't be found</returns>
        private int getBelowForm(Rectangle rect, dockMode dock, int ignoreIndex)
        {
            int index = -1;
            int top = int.MaxValue;

            for (int i = 0; i < formInfo.Count; i++)
            {
                if (formInfo[i].dock == dock && formInfo[i].form.Top > rect.Y && i != ignoreIndex)
                {
                    if (formInfo[i].form.Top < top)
                    {
                        top = formInfo[i].form.Top;
                        index = i;
                    }
                }
            }

            return index;
        }

        /// <summary>
        ///     Get the Form placed exactly at the left side of Rectangle passed to the function.
        ///     Returns -1 if it can't find any Form at the left.
        /// </summary>
        /// <param name="rect">The rectangle at where your form is located</param>
        /// <param name="dock">The docking mode that will be used</param>
        /// <param name="ignoreIndex">Index of the Form used to be ignored</param>
        /// <returns>Returns the Index for the Form on the list, or -1 if it can't be found</returns>
        private int getLeftForm(Rectangle rect, dockMode dock, int ignoreIndex)
        {
            int index = -1;
            int left = 0;

            for (int i = 0; i < formInfo.Count; i++)
            {
                if (formInfo[i].dock == dock && formInfo[i].form.Left < rect.X && i != ignoreIndex)
                {
                    if (formInfo[i].form.Left >= left)
                    {
                        left = formInfo[i].form.Left;
                        index = i;
                    }
                }
            }

            return index;
        }

        /// <summary>
        ///     Get the Form placed exactly at the right side of Rectangle passed to the function.
        ///     Returns -1 if it can't find any Form at the right.
        /// </summary>
        /// <param name="rect">The rectangle at where your form is located</param>
        /// <param name="dock">The docking mode that will be used</param>
        /// <param name="ignoreIndex">Index of the Form used to be ignored</param>
        /// <returns>Returns the Index for the Form on the list, or -1 if it can't be found</returns>
        private int getRightForm(Rectangle rect, dockMode dock, int ignoreIndex)
        {
            int index = -1;
            int left = int.MaxValue;

            for (int i = 0; i < formInfo.Count; i++)
            {
                if (formInfo[i].dock == dock && formInfo[i].form.Left > rect.X && i != ignoreIndex)
                {
                    if (formInfo[i].form.Left < left)
                    {
                        left = formInfo[i].form.Left;
                        index = i;
                    }
                }
            }

            return index;
        }

        /// <summary>
        ///     Return the Index of a Form on the list based on the custom-set Tag property.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        private int getFormInfoIndex(int index)
        {
            for (int i = 0; i < formInfo.Count; i++)
            {
                if (formInfo[i].index == index) return i;
            }

            return -1;
        }

        /// <summary>
        ///     Automatically arrange the Forms on a given dock side when a Form is removed.
        /// </summary>
        /// <param name="dock"></param>
        /// <param name="dockRect"></param>
        private void autoArrange(dockMode dock, Rectangle dockRect)
        {
            int top = dockRect.Top;
            int left = dockRect.Left;

            switch (dock)
            {
                case dockMode.Left:
                case dockMode.Right:
                    int indexRL = getTopmostForm(dock);
                    while (indexRL > -1)
                    {
                        int belowForm = getBelowForm(getRectangle(formInfo[indexRL].form), dock, indexRL);

                        formInfo[indexRL].form.Top = top;
                        formInfo[indexRL].form.Left = dockRect.Left;
                        formInfo[indexRL].form.Width = dockRect.Width;
                        if (belowForm > -1)
                        {
                            formInfo[indexRL].form.Height = (formInfo[belowForm].form.Top - formInfo[indexRL].form.Top) - gripSize;
                            formInfo[indexRL].hasGrip = true;
                        }
                        else
                        {
                            formInfo[indexRL].form.Height = ((dockRect.Y + dockRect.Height) - formInfo[indexRL].form.Top);
                            formInfo[indexRL].hasGrip = false;
                        }
                        top += formInfo[indexRL].form.Height + gripSize;
                        indexRL = belowForm;
                    }

                break;
                        
                case dockMode.Bottom:
                case dockMode.Top:
                    int indexBT = getLeftmostForm(dock);
                    while (indexBT > -1)
                    {
                        int rightForm = getRightForm(getRectangle(formInfo[indexBT].form), dock, indexBT);

                        formInfo[indexBT].form.Top = dockRect.Top;
                        formInfo[indexBT].form.Left = left;
                        formInfo[indexBT].form.Height = dockRect.Height;
                        if (rightForm > -1)
                        {
                            formInfo[indexBT].form.Width = (formInfo[rightForm].form.Left - formInfo[indexBT].form.Left) - gripSize;
                            formInfo[indexBT].hasGrip = true;
                        }
                        else
                        {
                            formInfo[indexBT].form.Width = ((dockRect.X + dockRect.Width) - formInfo[indexBT].form.Left);
                            formInfo[indexBT].hasGrip = false;
                        }
                        left += formInfo[indexBT].form.Width + gripSize;
                        indexBT = rightForm;
                    }

                break;
            }
        }

        /// <summary>
        ///     Counts the number of Forms on a given dock side.
        /// </summary>
        /// <param name="dock"></param>
        /// <returns></returns>
        private int formCount(dockMode dock)
        {
            int count = 0;
            for (int i = 0; i < formInfo.Count; i++)
            {
                if (formInfo[i].dock == dock) count++;
            }

            return count;
        }

        /// <summary>
        ///     Check if the control have any Form docked.
        /// </summary>
        /// <param name="dock"></param>
        /// <returns></returns>
        private bool hasDockedForm()
        {
            for (int i = 0; i < formInfo.Count; i++)
            {
                if (formInfo[i].dock != dockMode.Floating) return true;
            }
            return false;
        }

        /// <summary>
        ///     Get the first form from top to bottom of a given dock side.
        /// </summary>
        /// <param name="dock"></param>
        /// <returns></returns>
        private int getTopmostForm(dockMode dock)
        {
            int index = -1;
            int top = int.MaxValue;

            for (int i = 0; i < formInfo.Count; i++)
            {
                if (formInfo[i].dock == dock)
                {
                    if (formInfo[i].form.Top < top)
                    {
                        top = formInfo[i].form.Top;
                        index = i;
                    }
                }
            }

            return index;
        }

        /// <summary>
        ///     Get the last form from top to bottom of a given dock side.
        /// </summary>
        /// <param name="dock"></param>
        /// <returns></returns>
        private int getBottommostForm(dockMode dock)
        {
            int index = -1;
            int top = 0;

            for (int i = 0; i < formInfo.Count; i++)
            {
                if (formInfo[i].dock == dock)
                {
                    if (formInfo[i].form.Top >= top)
                    {
                        top = formInfo[i].form.Top;
                        index = i;
                    }
                }
            }

            return index;
        }

        /// <summary>
        ///     Get the first form from left to right of a given dock side.
        /// </summary>
        /// <param name="dock"></param>
        /// <returns></returns>
        private int getLeftmostForm(dockMode dock)
        {
            int index = -1;
            int left = int.MaxValue;

            for (int i = 0; i < formInfo.Count; i++)
            {
                if (formInfo[i].dock == dock)
                {
                    if (formInfo[i].form.Left < left)
                    {
                        left = formInfo[i].form.Left;
                        index = i;
                    }
                }
            }

            return index;
        }

        /// <summary>
        ///     Get the last form from left to right of a given dock side.
        /// </summary>
        /// <param name="dock"></param>
        /// <returns></returns>
        private int getRightmostForm(dockMode dock)
        {
            int index = -1;
            int left = 0;

            for (int i = 0; i < formInfo.Count; i++)
            {
                if (formInfo[i].dock == dock)
                {
                    if (formInfo[i].form.Left >= left)
                    {
                        left = formInfo[i].form.Left;
                        index = i;
                    }
                }
            }

            return index;
        }

        /// <summary>
        ///     Returns the index of the Form at the center, or -1 if it doesnt have one.
        /// </summary>
        /// <returns></returns>
        private int getCenterForm()
        {
            for (int i = 0; i < formInfo.Count; i++) //Center
            {
                if (formInfo[i].dock == dockMode.Center) return i;
            }

            return -1;
        }

        /// <summary>
        ///     Try to get enough space to create a dock side, reducing existing dock sides.
        /// </summary>
        /// <param name="dock"></param>
        /// <param name="newSpace"></param>
        /// <returns>Returns true if it get enough space or false if it doesn't</returns>
        private bool getSpaceToCreateDockSide(dockMode dock, int newSpace)
        {
            switch (dock)
            {
                case dockMode.Right:
                case dockMode.Left:
                    int bottomIndex = -1, topIndex = -1;
                    bool centerHasSpace = false;

                    int width = newSpace + gripSize;

                    //Primeiro verifica se é possivel encontrar um controle nos 3 docks do meio que dê para reduzir para caber o novo Dock Side
                    int bIndex = dock == dockMode.Right ? getRightmostForm(dockMode.Bottom) : getLeftmostForm(dockMode.Bottom); //Bottom
                    while (bIndex > -1)
                    {
                        if ((formInfo[bIndex].form.Width - width) >= minimumWidth)
                        {
                            bottomIndex = bIndex;
                            break;
                        }

                        bIndex = dock == dockMode.Right ? getLeftForm(getRectangle(formInfo[bIndex].form), dockMode.Bottom, bIndex) : getRightForm(getRectangle(formInfo[bIndex].form), dockMode.Bottom, bIndex);
                    }

                    int tIndex = dock == dockMode.Right ? getRightmostForm(dockMode.Top) : getLeftmostForm(dockMode.Top); //Top
                    while (tIndex > -1)
                    {
                        if ((formInfo[tIndex].form.Width - width) >= minimumWidth)
                        {
                            topIndex = tIndex;
                            break;
                        }

                        tIndex = dock == dockMode.Right ? getLeftForm(getRectangle(formInfo[tIndex].form), dockMode.Top, tIndex) : getRightForm(getRectangle(formInfo[tIndex].form), dockMode.Top, tIndex);
                    }

                    for (int i = 0; i < formInfo.Count; i++) //Center
                    {
                        if (formInfo[i].dock == dockMode.Center && (formInfo[i].form.Width - width) >= minimumWidth) centerHasSpace = true;
                    }

                    //Redimensiona e reposiciona os *Forms/Controles* para dar espaço ao novo Dock Side
                    bool result = (bottomIndex > -1 || formCount(dockMode.Bottom) == 0) && (topIndex > -1 || formCount(dockMode.Top) == 0) && (centerHasSpace || formCount(dockMode.Center) == 0);
                    if (width + (formCount(dockMode.Right) > 0 ? rightDockWidth : 0) + (formCount(dockMode.Left) > 0 ? leftDockWidth : 0) + gripSize * 2 > this.Width) result = false;

                    if (result)
                    {
                        if (dock == dockMode.Right) //Lado direito
                        {
                            if (bottomIndex > -1)
                            {
                                formInfo[bottomIndex].form.Width -= width;

                                int index = getRightForm(getRectangle(formInfo[bottomIndex].form), dockMode.Bottom, bottomIndex);
                                while (index > -1)
                                {
                                    formInfo[index].form.Left -= width;

                                    index = getRightForm(getRectangle(formInfo[index].form), dockMode.Bottom, index);
                                }
                            }

                            if (topIndex > -1)
                            {
                                formInfo[topIndex].form.Width -= width;

                                int index = getRightForm(getRectangle(formInfo[topIndex].form), dockMode.Top, topIndex);
                                while (index > -1)
                                {
                                    formInfo[index].form.Left -= width;

                                    index = getRightForm(getRectangle(formInfo[index].form), dockMode.Top, index);
                                }
                            }

                            if (formCount(dockMode.Center) > 0)
                            {
                                int index = getCenterForm();
                                formInfo[index].form.Width -= width;
                            }
                        }
                        else //Lado esquerdo
                        {
                            if (bottomIndex > -1)
                            {
                                formInfo[bottomIndex].form.Width -= width;
                                formInfo[bottomIndex].form.Left += width;

                                int index = getLeftForm(getRectangle(formInfo[bottomIndex].form), dockMode.Bottom, bottomIndex);
                                while (index > -1)
                                {
                                    formInfo[index].form.Left += width;

                                    index = getLeftForm(getRectangle(formInfo[index].form), dockMode.Bottom, index);
                                }
                            }

                            if (topIndex > -1)
                            {
                                formInfo[topIndex].form.Width -= width;
                                formInfo[topIndex].form.Left += width;

                                int index = getLeftForm(getRectangle(formInfo[topIndex].form), dockMode.Top, topIndex);
                                while (index > -1)
                                {
                                    formInfo[index].form.Left += width;

                                    index = getLeftForm(getRectangle(formInfo[index].form), dockMode.Top, index);
                                }
                            }

                            if (formCount(dockMode.Center) > 0)
                            {
                                int index = getCenterForm();
                                formInfo[index].form.Width -= width;
                                formInfo[index].form.Left += width;
                            }
                        }
                    }

                    return result;

                case dockMode.Bottom:
                    int bottomHeight = newSpace + gripSize;
                    bool hasBottom = formCount(dockMode.Top) == 0 || ((this.Height - topDockHeight) >= bottomHeight);
                    if (bottomHeight + (formCount(dockMode.Bottom) > 0 ? bottomDockHeight : 0) + (formCount(dockMode.Top) > 0 ? topDockHeight : 0) + gripSize * 2 > this.Height) hasBottom = false;

                    if (formCount(dockMode.Center) > 0)
                    {
                        int index = getCenterForm();
                        if (formInfo[index].form.Height - bottomHeight >= minimumHeight)
                        {
                            formInfo[index].form.Height -= bottomHeight;
                        }
                        else
                        {
                            hasBottom = false;
                        }
                    }

                    return hasBottom;
                case dockMode.Top:
                    int topHeight = newSpace + gripSize;
                    bool hasTop = formCount(dockMode.Bottom) == 0 || ((this.Height - bottomDockHeight) >= topHeight);
                    if (topHeight + (formCount(dockMode.Bottom) > 0 ? bottomDockHeight : 0) + (formCount(dockMode.Top) > 0 ? topDockHeight : 0) + gripSize * 2 > this.Height) hasTop = false;

                    if (formCount(dockMode.Center) > 0)
                    {
                        int index = getCenterForm();
                        if (formInfo[index].form.Height - topHeight >= minimumHeight)
                        {
                            formInfo[index].form.Height -= topHeight;
                            formInfo[index].form.Top += topHeight;
                        }
                        else
                        {
                            hasTop = false;
                        }
                    }

                    return hasTop;
            }

            return false;
        }

        /// <summary>
        ///     Get the Rectangle of the total area of a given dock side.
        /// </summary>
        /// <param name="dock"></param>
        /// <returns></returns>
        private Rectangle getDockRect(dockMode dock)
        {
            Rectangle rect = new Rectangle(0, 0, this.Width, this.Height);
            Rectangle rightDock = new Rectangle((rect.X + rect.Width) - rightDockWidth, rect.Y, rightDockWidth, rect.Height);
            Rectangle leftDock = new Rectangle(rect.X, rect.Y, leftDockWidth, rect.Height);
            Rectangle bottomDock = new Rectangle(rect.X, (rect.Y + rect.Height) - bottomDockHeight, rect.Width, bottomDockHeight);
            Rectangle topDock = new Rectangle(rect.X, rect.Y, rect.Width, topDockHeight);
            Rectangle centerDock = new Rectangle(rect.Location, rect.Size);

            //Redimensiona os espaços de dock de acordo com outros Dock Sides existentes
            if (formCount(dockMode.Right) > 0)
            {
                bottomDock.Width -= (rightDockWidth + gripSize);
                topDock.Width -= (rightDockWidth + gripSize);
                centerDock.Width -= (rightDockWidth + gripSize);
            }
            if (formCount(dockMode.Left) > 0)
            {
                bottomDock.X += leftDockWidth + gripSize;
                bottomDock.Width -= (leftDockWidth + gripSize);

                topDock.X += leftDockWidth + gripSize;
                topDock.Width -= (leftDockWidth + gripSize);

                centerDock.X += leftDockWidth + gripSize;
                centerDock.Width -= (leftDockWidth + gripSize);
            }
            if (formCount(dockMode.Bottom) > 0) centerDock.Height -= (bottomDockHeight + gripSize);
            if (formCount(dockMode.Top) > 0)
            {
                centerDock.Y += topDockHeight + gripSize;
                centerDock.Height -= (topDockHeight + gripSize);
            }

            switch (dock)
            {
                case dockMode.Right: return rightDock;
                case dockMode.Left: return leftDock;
                case dockMode.Bottom: return bottomDock;
                case dockMode.Top: return topDock;
                case dockMode.Center: return centerDock;
            }

            return Rectangle.Empty;
        }

        /// <summary>
        ///     Calculate the proportions of all windows and Dock Sides relative to the size of this control.
        ///     It is used when the control is resized.
        /// </summary>
        /// <param name="ignoreSize">Set this to true if you want to update the proportions even if the width/height is too small or equal 0</param>
        private void calculateProportions(bool ignoreSize = false)
        {
            Rectangle rect = new Rectangle(0, 0, this.Width, this.Height);

            for (int i = 0; i < formInfo.Count; i++)
            {
                int width = formInfo[i].form.Width;
                int height = formInfo[i].form.Height;

                switch (formInfo[i].dock)
                {
                    case dockMode.Right:
                    case dockMode.Left:
                        if (getBelowForm(getRectangle(formInfo[i].form), formInfo[i].dock, i) > -1) height += gripSize;
                        break;
                    case dockMode.Bottom:
                    case dockMode.Top:
                        if (getRightForm(getRectangle(formInfo[i].form), formInfo[i].dock, i) > -1) width += gripSize;
                        break;
                    case dockMode.Center:
                        if (formCount(dockMode.Right) > 0) width += gripSize;
                        if (formCount(dockMode.Left) > 0) width += gripSize;
                        if (formCount(dockMode.Bottom) > 0) height += gripSize;
                        if (formCount(dockMode.Top) > 0) height += gripSize;
                        break;
                }

                if (ignoreSize)
                {
                    formInfo[i].formProportions = new SizeF((float)width / rect.Width, (float)height / rect.Height);
                }
                else
                {
                    if (width >= minimumWidth && height >= minimumHeight) formInfo[i].formProportions = new SizeF((float)width / rect.Width, (float)height / rect.Height);
                }
            }

            rightDockProportionW = (float)rightDockWidth / rect.Width;
            leftDockProportionW = (float)leftDockWidth / rect.Width;
            bottomDockProportionH = (float)bottomDockHeight / rect.Height;
            topDockProportionH = (float)topDockHeight / rect.Height;
        }

        /// <summary>
        ///     Creates a rectangle from the Location/Size properties of a control.
        /// </summary>
        /// <param name="form"></param>
        /// <returns>The rectangle :P</returns>
        private Rectangle getRectangle(Control form)
        {
            return new Rectangle(form.Location, form.Size);
        }
    }
}