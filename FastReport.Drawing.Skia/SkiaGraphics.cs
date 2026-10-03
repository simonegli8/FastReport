using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using SkiaSharp;

namespace FastReport
{
    /// <summary>
    /// Drawing objects to a Skia canvas or a Skia-backed Bitmap.
    /// The Skia counterpart of <c>GdiGraphics</c>.
    /// </summary>
    public class SkiaGraphics : IGraphics
    {
        private Graphics graphics;
        private readonly bool haveToDispose;

        #region Properties
        public Graphics Graphics
        {
            get { return graphics; }
        }

        /// <summary>The underlying Skia canvas.</summary>
        public SKCanvas Canvas
        {
            get { return graphics.Canvas; }
        }

        float IGraphics.DpiX => this.graphics.DpiX;
        float IGraphics.DpiY => this.graphics.DpiY;

        TextRenderingHint IGraphics.TextRenderingHint { get => this.graphics.TextRenderingHint; set => this.graphics.TextRenderingHint = value; }
        InterpolationMode IGraphics.InterpolationMode { get => this.graphics.InterpolationMode; set => this.graphics.InterpolationMode = value; }
        SmoothingMode IGraphics.SmoothingMode { get => this.graphics.SmoothingMode; set => this.graphics.SmoothingMode = value; }
        System.Drawing.Drawing2D.Matrix IGraphics.Transform { get => this.graphics.Transform; set => this.graphics.Transform = value; }
        GraphicsUnit IGraphics.PageUnit { get => this.graphics.PageUnit; set => this.graphics.PageUnit = value; }
        bool IGraphics.IsClipEmpty => this.graphics.IsClipEmpty;
        Region IGraphics.Clip { get => this.graphics.Clip; set => this.graphics.Clip = value; }
        CompositingQuality IGraphics.CompositingQuality { get => this.graphics.CompositingQuality; set => this.graphics.CompositingQuality = value; }
        #endregion

        public SkiaGraphics(Image image)
            : this(Graphics.FromImage(image), true)
        {
        }

        public SkiaGraphics(SKCanvas canvas, float dpiX = 96f, float dpiY = 96f)
            : this(Graphics.FromCanvas(canvas, dpiX, dpiY), true)
        {
        }

        public SkiaGraphics(Graphics graphics, bool haveToDispose)
        {
            this.graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));
            this.haveToDispose = haveToDispose;
        }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing && haveToDispose)
                    graphics.Dispose();
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        #region Draw and measure text
        public void DrawString(string text, Font drawFont, Brush brush, float left, float top)
        {
            this.graphics.DrawString(text, drawFont, brush, left, top);
        }

        public void DrawString(string text, Font drawFont, Brush brush, RectangleF rectangleF)
        {
            this.graphics.DrawString(text, drawFont, brush, rectangleF);
        }

        public void DrawString(string text, Font font, Brush textBrush, RectangleF textRect, StringFormat format)
        {
            this.graphics.DrawString(text, font, textBrush, textRect, format);
        }

        public void DrawString(string text, Font font, Brush brush, float left, float top, StringFormat format)
        {
            this.graphics.DrawString(text, font, brush, left, top, format);
        }

        void IGraphics.DrawString(string s, Font font, Brush brush, PointF point, StringFormat format)
        {
            this.graphics.DrawString(s, font, brush, point, format);
        }

        public Region[] MeasureCharacterRanges(string text, Font font, RectangleF rect, StringFormat format)
        {
            return this.graphics.MeasureCharacterRanges(text, font, rect, format);
        }

        public SizeF MeasureString(string text, Font font, SizeF size)
        {
            return this.graphics.MeasureString(text, font, size);
        }

        public SizeF MeasureString(string text, Font font, int width, StringFormat format)
        {
            return this.graphics.MeasureString(text, font, width, format);
        }

        public void MeasureString(string text, Font font, SizeF size, StringFormat format, out int charsFit, out int linesFit)
        {
            this.graphics.MeasureString(text, font, size, format, out charsFit, out linesFit);
        }

        public SizeF MeasureString(string text, Font drawFont)
        {
            return this.graphics.MeasureString(text, drawFont);
        }

        public SizeF MeasureString(string text, Font font, SizeF layoutArea, StringFormat format)
        {
            return this.graphics.MeasureString(text, font, layoutArea, format);
        }
        #endregion

        #region Draw images
        public void DrawImage(Image image, float x, float y)
        {
            this.graphics.DrawImage(image, x, y, image.Width, image.Height);
        }

        public void DrawImage(Image image, RectangleF destRect, RectangleF srcRect, GraphicsUnit unit)
        {
            this.graphics.DrawImage(image, destRect, srcRect, unit);
        }

        public void DrawImage(Image image, RectangleF rect)
        {
            this.graphics.DrawImage(image, rect);
        }

        public void DrawImage(Image image, float x, float y, float width, float height)
        {
            this.graphics.DrawImage(image, x, y, width, height);
        }

        public void DrawImage(Image image, PointF[] points)
        {
            this.graphics.DrawImage(image, points);
        }

        public void DrawImage(Image image, Rectangle destRect, int srcX, int srcY, int srcWidth, int srcHeight, GraphicsUnit srcUnit, ImageAttributes imageAttr)
        {
            this.graphics.DrawImage(image, destRect, srcX, srcY, srcWidth, srcHeight, srcUnit, imageAttr);
        }

        public void DrawImage(Image image, Rectangle destRect, float srcX, float srcY, float srcWidth, float srcHeight, GraphicsUnit srcUnit, ImageAttributes imageAttrs)
        {
            this.graphics.DrawImage(image, destRect, srcX, srcY, srcWidth, srcHeight, srcUnit, imageAttrs);
        }

        public void DrawImageUnscaled(Image image, Rectangle rect)
        {
            this.graphics.DrawImageUnscaled(image, rect);
        }
        #endregion

        #region Draw geometry
        public void DrawArc(Pen pen, float x, float y, float width, float height, float startAngle, float sweepAngle)
        {
            this.graphics.DrawArc(pen, x, y, width, height, startAngle, sweepAngle);
        }

        public void DrawCurve(Pen pen, PointF[] points, int offset, int numberOfSegments, float tension)
        {
            this.graphics.DrawCurve(pen, points, offset, numberOfSegments, tension);
        }

        public void DrawEllipse(Pen pen, float left, float top, float width, float height)
        {
            this.graphics.DrawEllipse(pen, left, top, width, height);
        }

        public void DrawEllipse(Pen pen, RectangleF rect)
        {
            this.graphics.DrawEllipse(pen, rect);
        }

        public void DrawLine(Pen pen, float x1, float y1, float x2, float y2)
        {
            this.graphics.DrawLine(pen, x1, y1, x2, y2);
        }

        public void DrawLine(Pen pen, PointF p1, PointF p2)
        {
            this.graphics.DrawLine(pen, p1, p2);
        }

        public void DrawLines(Pen pen, PointF[] points)
        {
            this.graphics.DrawLines(pen, points);
        }

        public void DrawPath(Pen outlinePen, GraphicsPath path)
        {
            this.graphics.DrawPath(outlinePen, path);
        }

        public void DrawPie(Pen pen, float x, float y, float width, float height, float startAngle, float sweepAngle)
        {
            this.graphics.DrawPie(pen, x, y, width, height, startAngle, sweepAngle);
        }

        public void DrawPolygon(Pen pen, PointF[] points)
        {
            this.graphics.DrawPolygon(pen, points);
        }

        public void DrawPolygon(Pen pen, Point[] points)
        {
            this.graphics.DrawPolygon(pen, points);
        }

        public void DrawRectangle(Pen pen, float left, float top, float width, float height)
        {
            this.graphics.DrawRectangle(pen, left, top, width, height);
        }

        public void DrawRectangle(Pen pen, Rectangle rect)
        {
            this.graphics.DrawRectangle(pen, rect);
        }

        public void PathAddRectangle(GraphicsPath path, RectangleF rect)
        {
            path.AddRectangle(rect);
        }
        #endregion

        #region Fill geometry
        public void FillEllipse(Brush brush, float x, float y, float dx, float dy)
        {
            this.graphics.FillEllipse(brush, x, y, dx, dy);
        }

        public void FillEllipse(Brush brush, RectangleF rect)
        {
            this.graphics.FillEllipse(brush, rect);
        }

        public void FillPath(Brush brush, GraphicsPath path)
        {
            this.graphics.FillPath(brush, path);
        }

        public void FillPie(Brush brush, float x, float y, float width, float height, float startAngle, float sweepAngle)
        {
            this.graphics.FillPie(brush, x, y, width, height, startAngle, sweepAngle);
        }

        public void FillPolygon(Brush brush, PointF[] points)
        {
            this.graphics.FillPolygon(brush, points);
        }

        public void FillPolygon(Brush brush, Point[] points)
        {
            this.graphics.FillPolygon(brush, points);
        }

        public void FillRectangle(Brush brush, RectangleF rect)
        {
            this.graphics.FillRectangle(brush, rect);
        }

        public void FillRectangle(Brush brush, float left, float top, float width, float height)
        {
            this.graphics.FillRectangle(brush, left, top, width, height);
        }

        public void FillRegion(Brush brush, Region region)
        {
            this.graphics.FillRegion(brush, region);
        }
        #endregion

        #region Fill And Draw

        public void FillAndDrawPath(Pen pen, Brush brush, GraphicsPath path)
        {
            FillPath(brush, path);
            DrawPath(pen, path);
        }

        public void FillAndDrawEllipse(Pen pen, Brush brush, RectangleF rect)
        {
            FillEllipse(brush, rect);
            DrawEllipse(pen, rect);
        }

        public void FillAndDrawEllipse(Pen pen, Brush brush, float left, float top, float width, float height)
        {
            FillEllipse(brush, left, top, width, height);
            DrawEllipse(pen, left, top, width, height);
        }

        public void FillAndDrawPolygon(Pen pen, Brush brush, Point[] points)
        {
            FillPolygon(brush, points);
            DrawPolygon(pen, points);
        }

        public void FillAndDrawPolygon(Pen pen, Brush brush, PointF[] points)
        {
            FillPolygon(brush, points);
            DrawPolygon(pen, points);
        }

        public void FillAndDrawRectangle(Pen pen, Brush brush, float left, float top, float width, float height)
        {
            FillRectangle(brush, left, top, width, height);
            DrawRectangle(pen, left, top, width, height);
        }

        #endregion

        #region Transform
        public void MultiplyTransform(System.Drawing.Drawing2D.Matrix matrix, MatrixOrder order)
        {
            this.graphics.MultiplyTransform(matrix, order);
        }

        public void RotateTransform(float angle)
        {
            this.graphics.RotateTransform(angle);
        }

        public void ScaleTransform(float scaleX, float scaleY)
        {
            this.graphics.ScaleTransform(scaleX, scaleY);
        }

        public void TranslateTransform(float left, float top)
        {
            this.graphics.TranslateTransform(left, top);
        }
        #endregion

        #region State
        public void Restore(IGraphicsState state)
        {
            if (state is SkiaGraphicsState skiaState)
                this.graphics.Restore(skiaState.GraphicsState);
        }

        public IGraphicsState Save()
        {
            return new SkiaGraphicsState(this.graphics.Save());
        }
        #endregion

        #region Clip
        public bool IsVisible(RectangleF rect)
        {
            return this.graphics.IsVisible(rect);
        }

        public void ResetClip()
        {
            this.graphics.ResetClip();
        }

        public void SetClip(RectangleF rect)
        {
            this.graphics.SetClip(rect);
        }

        public void SetClip(RectangleF rect, CombineMode combineMode)
        {
            this.graphics.SetClip(rect, combineMode);
        }

        public void SetClip(GraphicsPath path, CombineMode combineMode)
        {
            this.graphics.SetClip(path, combineMode);
        }
        #endregion

        public class SkiaGraphicsState : IGraphicsState
        {
            public GraphicsState GraphicsState { get; }

            public SkiaGraphicsState(GraphicsState state)
            {
                GraphicsState = state;
            }
        }

        public static SkiaGraphics FromImage(Image image)
        {
            return new SkiaGraphics(image);
        }

        public static SkiaGraphics FromGraphics(Graphics graphics)
        {
            return new SkiaGraphics(graphics, false);
        }

        /// <summary>
        /// Wraps an existing canvas, e.g. a page of an <see cref="SKDocument"/> for PDF output.
        /// </summary>
        public static SkiaGraphics FromCanvas(SKCanvas canvas, float dpiX = 96f, float dpiY = 96f)
        {
            return new SkiaGraphics(canvas, dpiX, dpiY);
        }
    }
}
