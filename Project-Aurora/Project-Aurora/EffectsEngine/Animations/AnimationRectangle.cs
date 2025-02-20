﻿using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Aurora.EffectsEngine.Animations
{
    public class AnimationRectangle : AnimationFrame
    {
        public AnimationRectangle()
        {
            _dimension = new Rectangle((int)(0), (int)(0), (int)0, (int)0);
            _color = Utils.ColorUtils.GenerateRandomColor();
            _width = 1;
            _duration = 0.0f;
        }

        public AnimationRectangle(AnimationFrame animationFrame) : base(animationFrame)
        {
        }

        public AnimationRectangle(RectangleF dimension, Color color, int width = 1, float duration = 0.0f) : base(dimension, color, width, duration)
        {
        }

        public AnimationRectangle(PointF center, float rect_width, float rect_height, Color color, int width = 1, float duration = 0.0f)
        {
            _dimension = new RectangleF(center.X - rect_width * 0.5f, center.Y - rect_height * 0.5f, rect_width, rect_height);
            _color = color;
            _width = width;
            _duration = duration;
        }

        public AnimationRectangle(float x, float y, float rect_width, float rect_height, Color color, int width = 1, float duration = 0.0f)
        {
            _dimension = new RectangleF(x, y, rect_width, rect_height);
            _color = color;
            _width = width;
            _duration = duration;
        }

        public override void Draw(Graphics g)
        {
            if (_pen == null || _invalidated)
            {
                _pen = new Pen(_color);
                _pen.Width = _width;
                _pen.Alignment = System.Drawing.Drawing2D.PenAlignment.Center;

                virtUpdate();
                _invalidated = false;
            }


            g.ResetTransform();
            g.Transform = _transformationMatrix;
            g.DrawRectangle(_pen, _scaledDimension.X, _scaledDimension.Y, _scaledDimension.Width, _scaledDimension.Height);
        }

        public override AnimationFrame BlendWith(AnimationFrame otherAnim, double amount)
        {
            if (!(otherAnim is AnimationRectangle))
            {
                throw new FormatException("Cannot blend with another type");
            }
            AnimationRectangle otherCircle = (AnimationRectangle)otherAnim;

            amount = GetTransitionValue(amount);

            AnimationFrame newFrame = base.BlendWith(otherCircle, amount);

            return new AnimationRectangle(newFrame);
        }

        public override AnimationFrame GetCopy()
        {
            return new AnimationRectangle(this);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((AnimationRectangle)obj);
        }

        public bool Equals(AnimationRectangle p)
        {
            return _color.Equals(p._color) &&
                _dimension.Equals(p._dimension) &&
                _width.Equals(p._width) &&
                _duration.Equals(p._duration) &&
                _angle.Equals(p._angle);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + _color.GetHashCode();
                hash = hash * 23 + _dimension.GetHashCode();
                hash = hash * 23 + _width.GetHashCode();
                hash = hash * 23 + _duration.GetHashCode();
                hash = hash * 23 + _angle.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return $"AnimationRectangle [ Color: {_color.ToString()} Dimensions: {_dimension.ToString()} Width: {_width} Duration: {_duration} Angle: {_angle} ]";
        }
    }
}
