using System.Windows;

internal static class Int32RectExtensions
{
    internal static Int32Rect Truncate(this Int32Rect _this, Int32Rect container)
    {
        if (_this.Width > container.Width)
        {
            _this.Width = container.Width;
        }

        if (_this.X + _this.Width > container.Width)
        {
            _this.X = container.Width - _this.Width;
        }

        if (_this.Height > container.Height)
        {
            _this.Height = container.Height;
        }

        if (_this.Y + _this.Height > container.Height)
        {
            _this.Y = container.Height - _this.Height;
        }

        return _this;
    }
}