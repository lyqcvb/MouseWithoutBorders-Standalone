// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace MouseWithoutBorders
{
    public partial class ImageButton : PictureBox
    {
        public ImageButton()
        {
            InitializeComponent();
            UpdateEnabledState();
        }

        [Category("Appearance")]
        [Description("Image to show when Mouse is pressed on button")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Image DownImage { get; set; }

        private Image _normalImage;

        [Category("Appearance")]
        [Description("Image to show when button is in normal state")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Image NormalImage
        {
            get => _normalImage;
            set
            {
                _normalImage = value;
                UpdateEnabledState();
            }
        }

        [Category("Appearance")]
        [Description("Image to show when Mouse hovers over button")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Image HoverImage { get; set; }

        [Category("Appearance")]
        [Description("Image to show when button is disabled")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Image DisabledImage { get; set; }

        private bool _hovering;
        private bool _buttonDown;

        protected override void OnVisibleChanged(EventArgs e)
        {
            UpdateEnabledState();
            base.OnVisibleChanged(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            UpdateEnabledState();
            base.OnEnabledChanged(e);
        }

        protected override void OnLoadCompleted(AsyncCompletedEventArgs e)
        {
            UpdateEnabledState();
            base.OnLoadCompleted(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            _hovering = true;
            if (Enabled)
            {
                if (_buttonDown)
                {
                    if (DownImage != null && Image != DownImage)
                    {
                        Image = DownImage;
                    }
                }
                else
                {
                    Image = HoverImage ?? NormalImage;
                }
            }

            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _buttonDown = true;
            if (Enabled)
            {
                _ = Focus();
                if (DownImage != null)
                {
                    Image = DownImage;
                }
            }

            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _buttonDown = false;
            if (Enabled)
            {
                if (_hovering)
                {
                    if (HoverImage != null)
                    {
                        Image = HoverImage;
                    }
                }
                else
                {
                    Image = NormalImage;
                }
            }

            base.OnMouseUp(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovering = false;
            UpdateEnabledState();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            string caption = Name switch
            {
                "YesButton" => "Yes",
                "NoButton" => "No",
                "NextButton" => "Next",
                "BackButton" => "Back",
                "DoneButton" => "Done",
                "LinkButton" => "Link",
                "CloseButton" => "Close",
                _ => null,
            };
            if (Class.UiLanguage.Language == "zh-CN" && caption != null)
            {
                // These legacy buttons bake English words into their bitmap. Draw a native
                // button instead so its caption and disabled state follow the UI language.
                ControlPaint.DrawButton(pe.Graphics, ClientRectangle,
                    !Enabled ? ButtonState.Inactive : _buttonDown ? ButtonState.Pushed : ButtonState.Normal);
                TextRenderer.DrawText(pe.Graphics, Class.UiLanguage.Text(caption), Font, ClientRectangle,
                    Enabled ? SystemColors.ControlText : SystemColors.GrayText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            base.OnPaint(pe);
        }

        private void UpdateEnabledState()
        {
            if (Enabled)
            {
                Image = _hovering && HoverImage != null ? HoverImage : NormalImage;
            }
            else
            {
                if (DisabledImage != null)
                {
                    Image = DisabledImage;
                }
            }
        }
    }
}
