﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using SF.Space;
using System.Diagnostics;

namespace SF.Controls
{
    public class SpaceGridControl : RoundControl
    {
        public const double DefaultScale = 1E6;

        public const int IntegerMaxValue = (int.MaxValue / 8) * 7;
        public const int IntegerMinValue = (int.MinValue / 8) * 7;

        public PointF TextMisplacement = new PointF
        {
            X = 1.0F/8,
            Y = 1.0F/12,
        };

        public readonly PenSet VulnerableSectors =
            new PenSet
            {
                My = Pens.Firebrick,
                Friendly = Pens.DarkGray,
                Hostile = Pens.DarkGray,
            };

        public readonly PenSet MissileCircles =
            new PenSet
            {
                My = Pens.Navy,
                Friendly = Pens.DarkGray,
                Hostile = Pens.DarkRed,
            };

        public readonly PenSet ShipHulls =
            new PenSet
            {
                My = Pens.Navy,
                Friendly = Pens.Navy,
                Hostile = Pens.Firebrick,
            };

        public readonly BrushSet ShipNames =
            new BrushSet
            {
                My = Brushes.Black,
                Friendly = Brushes.Navy,
                Hostile = Brushes.Firebrick,
            };

        /// <summary>
        /// Kilometers per inch
        /// </summary>
        public double WorldScale
        {
            get { return this.m_scale; }
            set
            {
                if (this.m_scale == value)
                    return;
                if (value <= 0)
                    throw new ArgumentOutOfRangeException("Scale value should be positive.");
                this.m_scale = value;
                Invalidate();
            }
        }
        private double m_scale = DefaultScale;

        /// <summary>
        /// Coordinates of the center of the grid.
        /// </summary>
        public Vector Origin
        {
            get { return this.m_origin; }
            set
            {
                if (this.m_origin == value)
                    return;
                this.m_origin = value;
                Invalidate();
            }
        }
        private Vector m_origin;

        public bool StaticGrid
        {
            get { return this.m_staticGrid; }
            set
            {
                if (this.m_staticGrid == value)
                    return;
                this.m_staticGrid = value;
                Invalidate();
            }
        }
        private bool m_staticGrid = true;

        public bool Polar
        {
            get { return this.m_polar; }
            set
            {
                if (this.m_polar == value)
                    return;
                this.m_polar = value;
                Invalidate();
            }
        }
        private bool m_polar;

        public double Rotation
        {
            get { return m_rotation; }
            set
            {
                if (MathUtils.NearlyEqual(m_rotation, value))
                    return;
                m_rotation = value;
                Invalidate();
            }
        }
        private double m_rotation;

        public SpaceGridOptions Options { get; set; }

        private RectangleF m_client;

        public IShip OwnShip;
        public ICollection<IShip> Ships;
        public ICollection<IMissile> Missiles;
        public ICollection<Star> Stars;

        public class Curve : List<Vector>
        {
            public Pen Pencil;
        };

        public readonly List<Curve> Curves = new List<Curve>();

        public event EventHandler ShipSelected;

        private IShip m_selectedShip;
        public IShip SelectedShip
        {
            get
            {
                return m_selectedShip;
            }
            set
            {
                m_selectedShip = value;
                Invalidate();
                var handler = ShipSelected;
                if (handler != null)
                    handler(this, EventArgs.Empty);
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            SelectedShip = SelectShip(e.Location);
            base.OnMouseClick(e);
        }

        private IShip SelectShip(Point point)
        {
            Graphics g = this.CreateGraphics();
            var p = this.DeviceToWorld(g, point);
            var ships = Ships.ToList();
            if (OwnShip != null)
                ships.Add(OwnShip);
            if (ships.Count == 0)
                return null;
            ships = ships.Where(ship => (ship.S - p).Length < WorldScale / 2).ToList();
            if (ships.Count == 0)
                return null;
            if (ships.Count == 1)
                return ships.First();
            ships.Sort((a, b) => (a.S - p).SquareLength.CompareTo((b.S - p).SquareLength));
            if (SelectedShip == null)
                return ships.First();
            var i = ships.IndexOf(SelectedShip);
            if (i < 0)
                return ships.First();
            i = (i + 1) % ships.Count;
            return ships[i];
        }

        public PointF WorldToDevice(Graphics g, Vector v)
        {
            var p = ((v - Origin) / WorldScale).Rotate(-Rotation);
            p.X *= g.DpiX;
            p.Y *= -g.DpiY;
            p.X += m_center.X;
            p.Y += m_center.Y;
            var radius = Math.Max(Math.Abs(p.X), Math.Abs(p.Y));
            if (radius >= IntegerMaxValue)
                p = p * IntegerMaxValue / radius;
            return new PointF((float)p.X, (float)p.Y);
        }

        public Vector DeviceToWorld(Graphics g, Point p)
        {
            return new Vector
            {
                X = (p.X - m_center.X) / g.DpiX,
                Y = -(p.Y - m_center.Y) / g.DpiY,
            }.Rotate(Rotation) * WorldScale + Origin;
        }

        private float WorldToDevice(float dpi, double x)
        {
            var result = x * dpi / WorldScale;
            if (result <= IntegerMinValue)
                return IntegerMinValue;
            if (result >= IntegerMaxValue)
                return IntegerMaxValue;
            return (float)result;
        }

        protected override void DrawBackgroound(PaintEventArgs e)
        {
            m_client = new RectangleF(ClientRectangle.X, ClientRectangle.Y, ClientRectangle.Width - 1, ClientRectangle.Height - 1);
            e.Graphics.FillRectangle(WhitePaper, m_client);
            e.Graphics.DrawRectangles(BlackPen, new[] { m_client });
        }

        private void DrawGridLines(Graphics graphics)
        { 
            if (Options.HasFlag(SpaceGridOptions.NoGrid))
                return;
            var logScale = Math.Log10(WorldScale);
            float scale = (float)Math.Pow(10, Math.Ceiling(logScale) - logScale);
            float dpiX = scale * graphics.DpiX;
            float dpiY = scale * graphics.DpiY;
            if (Polar)
            {
                var n = (int) (m_client.Width / (2.0 * dpiX) + m_client.Height / (2.0 * dpiY));
                if (n < 2)
                    n = 2;
                for (var i = 1; i <= n; i++)
                    graphics.DrawEllipse(BlackPencil, m_center.X - i * dpiX, m_center.Y - i * dpiY, 2 * i * dpiX, 2 * i * dpiY);
                const int N = 12;
                for (int i = 1; i <= N; i++)
                {
                    var p = new PointF
                    {
                        X = (float)(m_center.X + dpiX * n * Math.Cos(2 * Math.PI * i / N)),
                        Y = (float)(m_center.Y + dpiY * n * Math.Sin(2 * Math.PI * i / N))
                    };
                    graphics.DrawLine(BlackPencil, m_center, p);
                    p = new PointF
                    {
                        X = (float)(m_center.X + dpiX * n * Math.Cos(2 * Math.PI * (i + 0.5) / N)),
                        Y = (float)(m_center.Y + dpiY * n * Math.Sin(2 * Math.PI * (i + 0.5) / N))
                    };
                    var q = new PointF
                    {
                        X = (float)(m_center.X + 2 * dpiX * Math.Cos(2 * Math.PI * (i + 0.5) / N)),
                        Y = (float)(m_center.Y + 2 * dpiY * Math.Sin(2 * Math.PI * (i + 0.5) / N))
                    };
                    graphics.DrawLine(BlackPencil, p, q);
                }
            }
            else
            {
                PointF center = m_center;
                if (StaticGrid)
                {
                    var dx = (float)(graphics.DpiX * Math.IEEERemainder(Origin.X, WorldScale * scale) / WorldScale);
                    var dy = (float)(graphics.DpiY * Math.IEEERemainder(Origin.Y, WorldScale * scale) / WorldScale);
                    center.X -= dx;
                    center.Y += dy;
                }
                var n = (int) (m_client.Width / (2 * dpiX)) + 1;
                for (var i = -n; i <= n; i++)
                {
                    var x = center.X + i * dpiX;
                    graphics.DrawLine(BlackPencil, x, m_client.Top, x, m_client.Bottom);
                }
                n = (int) (m_client.Height / (2 * dpiY)) + 1;
                for (var i = -n; i <= n; i++)
                {
                    var y = center.Y + i * dpiY;
                    graphics.DrawLine(BlackPencil, m_client.Left, y, m_client.Right, y);
                }
            }
        }

        protected override void DrawContents(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            m_client = new Rectangle(ClientRectangle.X, ClientRectangle.Y, ClientRectangle.Width - 1, ClientRectangle.Height - 1);
            DrawGridLines(e.Graphics);
            if (Stars != null && Stars.Count > 0)
                foreach (var s in Stars)
                    this.DrawStar(e.Graphics, s);
            if (Curves != null && Curves.Count > 0)
                foreach (var c in Curves)
                    DrawCurve(e.Graphics, c);
            if (Missiles != null && Missiles.Count > 0)
                foreach (var missile in Missiles)
                    DrawMissile(e.Graphics, missile);
            if (Ships != null && Ships.Count > 0)
                foreach (var ship in Ships)
                    DrawShip(e.Graphics, ship);
            if (OwnShip != null)
                DrawShip(e.Graphics, OwnShip);
            if (SelectedShip != null)
                DrawShipSelection(e.Graphics, SelectedShip);
        }

        private void DrawCurve(Graphics graphics, Curve curve)
        {
            if (curve.Count == 0)
                return;
            var points = new List<PointF>();
            foreach (var p in curve)
            {
                var q = WorldToDevice(graphics, p);
                if (points.Count != 0)
                {
                    var prev = points[points.Count - 1];
                    if (prev.X == q.X && prev.Y == q.Y)
                        continue;
                }
                points.Add(q);
            }
            if (points.Count > 1)
                graphics.DrawLines(curve.Pencil, points.ToArray());
            else
            {
                var q = points[0];
                points.Clear();
                points.Add(new PointF(q.X + 4, q.Y));
                points.Add(new PointF(q.X, q.Y + 4));
                points.Add(new PointF(q.X - 4, q.Y));
                points.Add(new PointF(q.X, q.Y - 4));
                graphics.DrawPolygon(curve.Pencil, points.ToArray());
            }
        }

        private bool IsVisible(RectangleF rect)
        {
            return m_client.IntersectsWith(rect);
        }

        private bool IsVisible(PointF point)
        {
            return m_client.Contains((int)point.X, (int)point.Y);
        }

        private bool IsVisible(PointF[] points)
        {
            return points.Any(p => m_client.Contains((int)p.X, (int)p.Y));
        }

        private void DrawStar(Graphics graphics, Star star)
        {
            var pen = SignalPen;
            var brush = BlackInk;
            var rx = Math.Max(WorldToDevice(graphics.DpiX, star.Radius), graphics.DpiX / 32);
            var ry = Math.Max(WorldToDevice(graphics.DpiY, star.Radius), graphics.DpiY / 32);
            var p = WorldToDevice(graphics, star.Position);
            var rect = new RectangleF(p.X - rx, p.Y - ry, 2 * rx, 2 * ry);
            if (IsVisible(rect))
            {
                graphics.DrawEllipse(pen, rect);
            }
            WorldDrawText(graphics, brush, star.Position, star.Name);
        }

        private void DrawShip(Graphics graphics, IShip ship)
        {
            DrawVulnerableSectors(graphics, ship);
            DrawShipWedge(graphics, ship);
            if (ship.Board() > 0.5)
                DrawMissileCircle(graphics, ship);
            DrawShipHull(graphics, ship);
            var brush = ShipNames.Select(OwnShip, ship);
            WorldDrawText(graphics, brush, ship.S, ship.Name);
        }

        private void DrawVulnerableSectors(Graphics graphics, IShip ship)
        {
            var pen = VulnerableSectors.Select(OwnShip, ship);
            bool isMyShip = ship == OwnShip;
            bool isFriendlyShip = !isMyShip && (OwnShip != null && OwnShip.Nation == ship.Nation);
            bool isHostileShip = (OwnShip != null && OwnShip.Nation != ship.Nation);
            var range = (isMyShip || isFriendlyShip || OwnShip == null || OwnShip != null) ?
                Catalog.Instance.MaximumMissileRange : this.OwnShip.MissileRange();
            if (Options.HasFlag(SpaceGridOptions.FriendlySectorsByMyMissileRange) && OwnShip != null && OwnShip.Class != null)
                range = OwnShip.MissileRange();
            if ((isMyShip && !Options.HasFlag(SpaceGridOptions.MyVulnerableSectors)) ||
                (isFriendlyShip && !Options.HasFlag(SpaceGridOptions.FriendlyVulnerableSectors)) ||
                (isHostileShip && !Options.HasFlag(SpaceGridOptions.HostileVulnerableSectors)))
                return;
            WorldDrawPie(graphics, pen, ship.S, range, ship.Heading, Catalog.Instance.ThroatAngle);
            WorldDrawPie(graphics, pen, ship.S, range, ship.Heading - Math.PI, Catalog.Instance.SkirtAngle);
        }

        private void DrawMissileCircle(Graphics graphics, IShip ship)
        {
            bool isMyShip = ship == OwnShip;
            bool isFriendlyShip = !isMyShip && (OwnShip != null && OwnShip.Nation == ship.Nation);
            bool isHostileShip = (OwnShip != null && OwnShip.Nation != ship.Nation);
            if ((isMyShip && !Options.HasFlag(SpaceGridOptions.MyMissileCircles)) ||
                (isFriendlyShip && !Options.HasFlag(SpaceGridOptions.FriendlyMissileCircles)) ||
                (isHostileShip && !Options.HasFlag(SpaceGridOptions.HostileMissileCircles)))
                return;
            var pen = MissileCircles.Select(OwnShip, ship);
            var range = (OwnShip != null && OwnShip.Nation != ship.Nation) ? Catalog.Instance.MaximumMissileRange : ship.MissileRange();
            WorldDrawCircle(graphics, pen, ship.S, range);
        }

        private void DrawShipSelection(Graphics graphics, IShip ship)
        {
            float size = 1.0F / 4;
            var pen = BlackPen;
            var position = WorldToDevice(graphics, ship.S);
            var rect = new RectangleF
            {
                X = position.X - size * graphics.DpiX,
                Y = position.Y - size * graphics.DpiY,
                Width = 2 * size * graphics.DpiX,
                Height = 2 * size * graphics.DpiY,
            };
            graphics.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
        }
        
        private void DrawShipWedge(Graphics graphics, IShip ship)
        {
            if (ship.Board() > 0.5)
                return;
            double size = WorldScale / 4;
            var pen = SignalPen;
            var points = new[]
                {
                    WorldToDevice(graphics, ship.S + Vector.Direction(ship.Heading + Math.PI / 4) * size),
                    WorldToDevice(graphics, ship.S + Vector.Direction(ship.Heading + Math.PI * 11 / 12) * size),
                    WorldToDevice(graphics, ship.S + Vector.Direction(ship.Heading - Math.PI * 11 / 12) * size),
                    WorldToDevice(graphics, ship.S + Vector.Direction(ship.Heading - Math.PI / 4) * size),
                };
            if (!IsVisible(points))
                return;
            graphics.DrawLine(pen, points[0], points[1]);
            graphics.DrawLine(pen, points[2], points[3]);
        }

        private void DrawShipHull(Graphics graphics, IShip ship)
        {
            const double alpha = Math.PI * 11 / 12;
            double size = WorldScale / 6;
            var pen = ShipHulls.Select(OwnShip, ship);
            var points = new[]
                {
                    WorldToDevice(graphics, ship.S + Vector.Direction(ship.Heading) * size),
                    WorldToDevice(graphics, ship.S + Vector.Direction(ship.Heading + alpha) * size),
                    WorldToDevice(graphics, ship.S + Vector.Direction(ship.Heading - alpha) * size),
                };
            if (!IsVisible(points))
                return;
            graphics.DrawPolygon(pen, points);
        }

        private void DrawMissile(Graphics graphics, IMissile missile)
        {
            //const double alpha = Math.PI * 11 / 12;
            double size = WorldScale / 6;// / 8;
            var pen = SignalPen;
                //OwnShip != null && missile.Nation == OwnShip.Nation ? ShipHulls.Friendly : ShipHulls.Hostile;
            var a = missile.A;
            if (a.Length > MathUtils.Epsilon)
                a.Length = size;
            var points = new[]
                {
                    WorldToDevice(graphics, missile.S),
                    WorldToDevice(graphics, missile.S + a),
                    WorldToDevice(graphics, missile.S + a.Rotate(Math.PI/2) / 3),
                    WorldToDevice(graphics, missile.S - a.Rotate(Math.PI/2) / 3),
                };
            if (!IsVisible(points))
                return;
            graphics.DrawLine(pen, points[0], points[1]);
            graphics.DrawLine(pen, points[2], points[3]);
        }

        private void WorldDrawText(Graphics graphics, Brush brush, Vector origin, string text)
        {
            var p = WorldToDevice(graphics, origin);
            p = new PointF
            {
                X = p.X + TextMisplacement.X * graphics.DpiX,
                Y = p.Y + TextMisplacement.Y * graphics.DpiY,
            };
            if (IsVisible(p))
                graphics.DrawString(text, Font, brush, p);
        }

        private void WorldDrawCircle(Graphics graphics, Pen pen, Vector origin, double radius)
        {
            var rx = WorldToDevice(graphics.DpiX, radius);
            var ry = WorldToDevice(graphics.DpiY, radius);
            var p = WorldToDevice(graphics, origin);
            if (rx <= 0 || ry <= 0)
                return;
            var rect = new RectangleF(p.X - rx, p.Y - ry, 2 * rx, 2 * ry);
            if (IsVisible(rect))
                graphics.DrawEllipse(pen, rect);
        }

        private void WorldDrawPie(Graphics graphics, Pen pen, Vector origin, double radius, double medianAngle, double sweepAngle)
        {
            var rx = WorldToDevice(graphics.DpiX, radius);
            var ry = WorldToDevice(graphics.DpiY, radius);
            var p = WorldToDevice(graphics, origin);
            if (rx <= 0 || ry <= 0)
                return;
            var rect = new RectangleF(p.X - rx, p.Y - ry, 2 * rx, 2 * ry);
            if (IsVisible(rect))
                graphics.DrawPie(pen, rect,
                    (float)MathUtils.ToDegrees(medianAngle - sweepAngle/ 2 - Math.PI / 2 - Rotation),
                    (float)MathUtils.ToDegrees(sweepAngle));
        }
    }

    [Flags]
    public enum SpaceGridOptions
    {
        None = 0,
        NoGrid = 0x01,
        MyMissileCircles = 0x02,
        FriendlyMissileCircles = 0x04,
        HostileMissileCircles = 0x08,
        MyVulnerableSectors = 0x10,
        FriendlyVulnerableSectors = 0x20,
        HostileVulnerableSectors = 0x40,
        FriendlySectorsByMyMissileRange = 0x1000
    };

    public class PenSet
    {
        public Pen Default = Pens.Black;
        public Pen My { get; set; }
        public Pen Friendly { get; set; }
        public Pen Hostile { get; set; }

        public Pen Select(IShip OwnShip, IShip ship)
        {
            if (ship == OwnShip)
                return My;
            if (OwnShip != null && ship.Nation == OwnShip.Nation)
                return Friendly;
            if (OwnShip != null && ship.Nation != OwnShip.Nation)
                return Hostile;
            return Default;
        }
    }

    public class BrushSet
    {
        public Brush Default = Brushes.Black;
        public Brush My { get; set; }
        public Brush Friendly { get; set; }
        public Brush Hostile { get; set; }

        public Brush Select(IShip OwnShip, IShip ship)
        {
            if (ship == OwnShip)
                return My;
            if (OwnShip != null && ship.Nation == OwnShip.Nation)
                return Friendly;
            if (OwnShip != null && ship.Nation != OwnShip.Nation)
                return Hostile;
            return Default;
        }
    }
}
