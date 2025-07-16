using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Animate
{
    internal class Animation
    {
        public BitmapImage SpriteSheet { get; private set; }
        public int FrameWidth { get; private set; }
        public int FrameHeight { get; private set; }
        public int FrameCount => SpriteSheet != null ? SpriteSheet.PixelWidth / FrameWidth : 0;
        public int CurrentFrame { get; private set; }

        private int frameInterval = 100; // ms
        private int elapsedTime = 0;

        public Animation(BitmapImage spriteSheet, int frameWidth, int frameHeight)
        {
            SpriteSheet = spriteSheet;
            FrameWidth = frameWidth;
            FrameHeight = frameHeight;
        }

        public void Update(int deltaTime)
        {
            if (FrameCount == 0) return;

            elapsedTime += deltaTime;
            if (elapsedTime >= frameInterval)
            {
                elapsedTime = 0;
                CurrentFrame = (CurrentFrame + 1) % FrameCount;
            }
        }

        public CroppedBitmap GetCurrentFrame()
        {
            if (SpriteSheet == null) return null;

            return new CroppedBitmap(SpriteSheet, new Int32Rect(CurrentFrame * FrameWidth, 0, FrameWidth, FrameHeight));
        }

        public void Reload(BitmapImage newSprite)
        {
            SpriteSheet = newSprite;
            CurrentFrame = 0;
        }
    }
}
