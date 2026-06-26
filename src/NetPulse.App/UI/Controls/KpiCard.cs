using System;
using System.Drawing;
using System.Windows.Forms;

namespace NetPulse.App.UI.Controls
{
    public class KpiCard : UserControl
    {
        private readonly Label _lblTitle;
        private readonly Label _lblValue;
        private readonly Label _lblSubtitle;
        private readonly System.Windows.Forms.Timer _flashTimer;
        private bool _flashState;
        private bool _isFlashing;

        private Color _normalBgColor = Color.FromArgb(30, 30, 30);
        private Color _flashBgColor = Color.FromArgb(80, 20, 20);
        private Color _valueColor = Color.White;

        public string Title
        {
            get => _lblTitle.Text;
            set => _lblTitle.Text = value;
        }

        public string Value
        {
            get => _lblValue.Text;
            set => _lblValue.Text = value;
        }

        public string Subtitle
        {
            get => _lblSubtitle.Text;
            set => _lblSubtitle.Text = value;
        }

        public Color ValueColor
        {
            get => _lblValue.ForeColor;
            set
            {
                _lblValue.ForeColor = value;
                _valueColor = value;
            }
        }

        public bool IsFlashing
        {
            get => _isFlashing;
            set
            {
                if (_isFlashing == value) return;
                _isFlashing = value;
                if (_isFlashing)
                {
                    _flashTimer.Start();
                }
                else
                {
                    _flashTimer.Stop();
                    BackColor = _normalBgColor;
                    _lblValue.ForeColor = _valueColor;
                }
            }
        }

        public KpiCard()
        {
            Size = new Size(200, 100);
            BackColor = _normalBgColor;
            Padding = new Padding(10);
            DoubleBuffered = true;

            _lblTitle = new Label
            {
                Text = "KPI TITLE",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(180, 180, 180),
                Location = new Point(10, 10),
                AutoSize = true
            };

            _lblValue = new Label
            {
                Text = "Value",
                Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(10, 30),
                Size = new Size(180, 35),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _lblSubtitle = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                ForeColor = Color.FromArgb(140, 140, 140),
                Location = new Point(10, 70),
                Size = new Size(180, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };

            Controls.Add(_lblTitle);
            Controls.Add(_lblValue);
            Controls.Add(_lblSubtitle);

            _flashTimer = new System.Windows.Forms.Timer();
            _flashTimer.Interval = 500;
            _flashTimer.Tick += FlashTimer_Tick;

            Resize += KpiCard_Resize;
        }

        private void KpiCard_Resize(object? sender, EventArgs e)
        {
            _lblValue.Width = Width - 20;
            _lblSubtitle.Width = Width - 20;
            _lblSubtitle.Top = Height - 25;
        }

        private void FlashTimer_Tick(object? sender, EventArgs e)
        {
            if (_flashState)
            {
                BackColor = _flashBgColor;
                _lblValue.ForeColor = Color.White;
            }
            else
            {
                BackColor = _normalBgColor;
                _lblValue.ForeColor = Color.Red;
            }
            _flashState = !_flashState;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Color.FromArgb(50, 50, 50), 1))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }
    }
}
