﻿using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Foster.Framework;

/// <summary>
/// A 2D Integer Rectangle
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct RectInt(int x, int y, int w, int h) : IEquatable<RectInt>
{
	public int X = x;
	public int Y = y;
	public int Width = w;
	public int Height = h;

	public Point2 Position
	{
		readonly get => new(X, Y);
		set
		{
			X = value.X;
			Y = value.Y;
		}
	}

	public Point2 Size
	{
		readonly get => new(Width, Height);
		set
		{
			Width = value.X;
			Height = value.Y;
		}
	}

	public readonly int Area => Width * Height;

	#region Edges

	public int Left
	{
		readonly get => X;
		set => X = value;
	}

	public int Right
	{
		readonly get => X + Width;
		set => X = value - Width;
	}

	public int CenterX
	{
		readonly get => X + Width / 2;
		set => X = value - Width / 2;
	}

	public int Top
	{
		readonly get => Y;
		set => Y = value;
	}

	public int Bottom
	{
		readonly get => Y + Height;
		set => Y = value - Height;
	}

	public int CenterY
	{
		readonly get => Y + Height / 2;
		set => Y = value - Height / 2;
	}

	#endregion

	#region Points

	public readonly Point2 Min => new(Math.Min(X, Right), Math.Min(Y, Bottom));
	public readonly Point2 Max => new(Math.Max(X, Right), Math.Max(Y, Bottom));

	public Point2 TopLeft
	{
		readonly get => new(Left, Top);
		set
		{
			Left = value.X;
			Top = value.Y;
		}
	}

	public Point2 TopCenter
	{
		readonly get => new(CenterX, Top);
		set
		{
			CenterX = value.X;
			Top = value.Y;
		}
	}

	public Point2 TopRight
	{
		readonly get => new(Right, Top);
		set
		{
			Right = value.X;
			Top = value.Y;
		}
	}

	public Point2 CenterLeft
	{
		readonly get => new(Left, CenterY);
		set
		{
			Left = value.X;
			CenterY = value.Y;
		}
	}

	public Point2 Center
	{
		readonly get => new(CenterX, CenterY);
		set
		{
			CenterX = value.X;
			CenterY = value.Y;
		}
	}

	public Point2 CenterRight
	{
		readonly get => new(Right, CenterY);
		set
		{
			Right = value.X;
			CenterY = value.Y;
		}
	}

	public Point2 BottomLeft
	{
		readonly get => new(Left, Bottom);
		set
		{
			Left = value.X;
			Bottom = value.Y;
		}
	}

	public Point2 BottomCenter
	{
		readonly get => new(CenterX, Bottom);
		set
		{
			CenterX = value.X;
			Bottom = value.Y;
		}
	}

	public Point2 BottomRight
	{
		readonly get => new(Right, Bottom);
		set
		{
			Right = value.X;
			Bottom = value.Y;
		}
	}

	#endregion

	#region PointsF

	public readonly float CenterXF => X + Width * .5f;
	public readonly float CenterYF => Y + Height * .5f;
	public readonly Vector2 TopCenterF => new(CenterXF, Top);
	public readonly Vector2 CenterLeftF => new(Left, CenterYF);
	public readonly Vector2 CenterF => new(CenterXF, CenterYF);
	public readonly Vector2 CenterRightF => new(Right, CenterYF);
	public readonly Vector2 BottomCenterF => new(CenterXF, Bottom);

	#endregion

	public RectInt(int w, int h)
		: this(0, 0, w, h)
	{

	}

	public RectInt(in Point2 pos, int w, int h)
		: this(pos.X, pos.Y, w, h)
	{

	}

	public RectInt(in Point2 pos, in Point2 size)
		: this(pos.X, pos.Y, size.X, size.Y)
	{
		
	}

	#region Collision

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly bool Contains(in Point2 point)
		=> (point.X >= X && point.Y >= Y && point.X < X + Width && point.Y < Y + Height);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly bool Contains(in Vector2 vec)
		=> (vec.X >= X && vec.Y >= Y && vec.X < X + Width && vec.Y < Y + Height);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly bool Contains(in RectInt rect)
		=> (Left < rect.Left && Top < rect.Top && Bottom > rect.Bottom && Right > rect.Right);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly bool Overlaps(in RectInt against)
		=> X + Width > against.X && Y + Height > against.Y && X < against.X + against.Width && Y < against.Y + against.Height;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly bool Overlaps(in Rect against)
		=> X + Width > against.X && Y + Height > against.Y && X < against.X + against.Width && Y < against.Y + against.Height;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly RectInt Conflate(in RectInt other)
	{
		var min = Point2.Min(Min, other.Min);
		var max = Point2.Max(Max, other.Max);
		return new(min.X, min.Y, max.X - min.X, max.Y - min.Y);
	}

	/// <summary>
	/// Get the rectangle intersection of two rectangles
	/// </summary>
	public readonly RectInt GetIntersection(in RectInt against)
	{
		bool overlapX = X + Width > against.X && X < against.X + against.Width;
		bool overlapY = Y + Height > against.Y && Y < against.Y + against.Height;

		RectInt r = new();

		if (overlapX)
		{
			r.Left = Math.Max(Left, against.Left);
			r.Width = Math.Min(Right, against.Right) - r.Left;
		}

		if (overlapY)
		{
			r.Top = Math.Max(Top, against.Top);
			r.Height = Math.Min(Bottom, against.Bottom) - r.Top;
		}

		return r;
	}

	/// <summary>
	/// Return the sector that the point falls within (see diagram in comments below). A result of zero indicates a point inside the rectangle
	/// </summary>
	//  0101 | 0100 | 0110
	// ------+------+------
	//  0001 | 0000 | 0010
	// ------+------+------
	//  1001 | 1000 | 1010
	public readonly byte GetPointSector(in Vector2 pt)
	{
		byte sector = 0;
		if (pt.X < X)
			sector |= 0b0001;
		else if (pt.X >= X + Width)
			sector |= 0b0010;
		if (pt.Y < Y)
			sector |= 0b0100;
		else if (pt.Y >= Y + Height)
			sector |= 0b1000;
		return sector;
	}

	public readonly bool Overlaps(in Line line)
	{
		var secA = GetPointSector(line.From);
		var secB = GetPointSector(line.To);

		if (secA == 0 || secB == 0)
			return true;
		else if ((secA & secB) != 0)
			return false;
		else
		{
			// Do line checks against the edges
			var both = secA | secB;

			// top check
			if ((both & 0b0100) != 0
			&& line.Intersects(new Line(TopLeft, TopRight)))
				return true;

			// bottom check
			if ((both & 0b1000) != 0
			&& line.Intersects(new Line(BottomLeft, BottomRight)))
				return true;

			// left edge check
			if ((both & 0b0001) != 0
			&& line.Intersects(new Line(TopLeft, BottomLeft)))
				return true;

			// right edge check
			if ((both & 0b0010) != 0
			&& line.Intersects(new Line(TopRight, BottomRight)))
				return true;

			return false;
		}
	}

	public readonly bool Overlaps(in LineInt line)
	{
		var secA = GetPointSector(line.From);
		var secB = GetPointSector(line.To);

		if (secA == 0 || secB == 0)
			return true;
		else if ((secA & secB) != 0)
			return false;
		else
		{
			// Do line checks against the edges
			var both = secA | secB;

			// top check
			if ((both & 0b0100) != 0
			&& line.Intersects(new LineInt(TopLeft, TopRight)))
				return true;

			// bottom check
			if ((both & 0b1000) != 0
			&& line.Intersects(new LineInt(BottomLeft, BottomRight)))
				return true;

			// left edge check
			if ((both & 0b0001) != 0
			&& line.Intersects(new LineInt(TopLeft, BottomLeft)))
				return true;

			// right edge check
			if ((both & 0b0010) != 0
			&& line.Intersects(new LineInt(TopRight, BottomRight)))
				return true;

			return false;
		}
	}

	#endregion

	#region Transform

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly RectInt At(in Point2 pos)
		=> new(pos.X, pos.Y, Width, Height);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly RectInt Inflate(int by)
		=> new(X - by, Y - by, Width + by * 2, Height + by * 2);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly RectInt Inflate(int byX, int byY)
		=> new(X - byX, Y - byY, Width + byX * 2, Height + byY * 2);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly RectInt Inflate(in Point2 by)
		=> Inflate(by.X, by.Y);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly Rect Inflate(float by)
		=> new(X - by, Y - by, Width + by * 2, Height + by * 2);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly Rect Inflate(float byX, float byY)
		=> new(X - byX, Y - byY, Width + byX * 2, Height + byY * 2);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly Rect Inflate(in Vector2 by)
		=> Inflate(by.X, by.Y);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly RectInt Translate(int byX, int byY)
		=> new(X + byX, Y + byY, Width, Height);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly RectInt Translate(in Point2 by)
		=> new(X + by.X, Y + by.Y, Width, Height);

	public readonly RectInt ScaleX(int scale)
	{
		var r = new RectInt(X * scale, Y, Width * scale, Height);

		if (r.Width < 0)
		{
			r.X += r.Width;
			r.Width *= -1;
		}

		return r;
	}

	public readonly RectInt ScaleY(int scale)
	{
		var r = new RectInt(X, Y * scale, Width, Height * scale);

		if (r.Height < 0)
		{
			r.Y += r.Height;
			r.Height *= -1;
		}

		return r;
	}

	public readonly RectInt RotateLeft(in Point2 origin)
	{
		Point2 a = (TopLeft - origin).TurnLeft();
		Point2 b = (TopRight - origin).TurnLeft();
		Point2 c = (BottomRight - origin).TurnLeft();
		Point2 d = (BottomLeft - origin).TurnLeft();
		Point2 min = Point2.Min(a, b, c, d);
		Point2 max = Point2.Max(a, b, c, d);
		return new(min.X, min.Y, max.X - min.X, max.Y - min.Y);
	}

	public readonly RectInt RotateLeft(in Point2 origin, int count)
	{
		RectInt r = this;
		while (count-- > 0)
			r = r.RotateLeft(origin);
		return r;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly RectInt RotateLeft() => RotateLeft(Point2.Zero);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly RectInt RotateLeft(int count) => RotateLeft(Point2.Zero, count);

	public readonly RectInt RotateRight(in Point2 origin)
	{
		Point2 a = (TopLeft - origin).TurnRight();
		Point2 b = (TopRight - origin).TurnRight();
		Point2 c = (BottomRight - origin).TurnRight();
		Point2 d = (BottomLeft - origin).TurnRight();
		Point2 min = Point2.Min(a, b, c, d);
		Point2 max = Point2.Max(a, b, c, d);
		return new(min.X, min.Y, max.X - min.X, max.Y - min.Y);
	}
	public readonly RectInt RotateRight(in Point2 origin, int count)
	{
		RectInt r = this;
		while (count-- > 0)
			r = r.RotateRight(origin);
		return r;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly RectInt RotateRight() => RotateRight(Point2.Zero);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly RectInt RotateRight(int count) => RotateRight(Point2.Zero, count);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly RectInt Rotate(Cardinal direction) => RotateRight(direction.Value);

	/// <summary>
	/// Resolve negative width or height to an equivalent rectangle with positive width and height. Ex: (0, 0, -2, -3) validates to (-2, -3, 2, 3)
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly RectInt ValidateSize()
	{
		RectInt rect = this;

		if (Width < 0)
		{
			rect.X += Width;
			rect.Width *= -1;
		}

		if (Height < 0)
		{
			rect.Y += Height;
			rect.Height *= -1;
		}

		return rect;
	}

	public readonly RectInt GetSweep(Cardinal direction, int distance)
	{
		if (distance < 0)
		{
			distance *= -1;
			direction = direction.Reverse;
		}

		if (direction == Cardinal.Right)
			return new(X + Width, Y, distance, Height);
		else if (direction == Cardinal.Left)
			return new(X - distance, Y, distance, Height);
		else if (direction == Cardinal.Down)
			return new(X, Y + Height, Width, distance);
		else
			return new(X, Y - distance, Width, distance);
	}

	#endregion

	/// <summary>
	/// Get the rect as a tuple of integers
	/// </summary>
	public readonly (int X, int Y, int Width, int Height) Deconstruct() => (X, Y, Width, Height);

	public static RectInt Centered(Point2 center, Point2 size)
		=> new(center.X - size.X / 2, center.Y - size.Y / 2, size.X, size.Y);

	public static RectInt Between(Point2 a, Point2 b)
	{
		RectInt rect;

		rect.X = a.X < b.X ? a.X : b.X;
		rect.Y = a.Y < b.Y ? a.Y : b.Y;
		rect.Width = (a.X > b.X ? a.X : b.X) - rect.X;
		rect.Height = (a.Y > b.Y ? a.Y : b.Y) - rect.Y;

		return rect;
	}

	/// <summary>
	/// Enumerate all integer positions within this rectangle
	/// </summary>
	public readonly IEnumerable<Point2> AllPoints
	{
		get
		{
			for (int x = X; x < X + Width; x++)
				for (int y = Y; y < Y + Height; y++)
					yield return new(x, y);
		}
	}

	public readonly bool Equals(RectInt other) => this == other;
	public override readonly bool Equals(object? obj) => (obj is RectInt other) && (this == other);	
	public override readonly int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
	public override readonly string ToString() => $"[{X}, {Y}, {Width}, {Height}]";

	public static implicit operator RectInt((int X, int Y, int Width, int Height) tuple) => new(tuple.X, tuple.Y, tuple.Width, tuple.Height);
	public static implicit operator Rect(in RectInt rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

	public static bool operator ==(RectInt a, RectInt b) => a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height;
	public static bool operator !=(RectInt a, RectInt b) => !(a == b);
	public static RectInt operator +(in RectInt a, in Point2 b) => new(a.X + b.X, a.Y + b.Y, a.Width, a.Height);
	public static RectInt operator -(in RectInt a, in Point2 b) => new(a.X - b.X, a.Y - b.Y, a.Width, a.Height);
	public static RectInt operator *(in RectInt rect, int scaler)
		=> new RectInt(rect.X * scaler, rect.Y * scaler, rect.Width * scaler, rect.Height * scaler).ValidateSize();
	public static RectInt operator /(in RectInt rect, int scaler)
		=> new RectInt(rect.X / scaler, rect.Y / scaler, rect.Width / scaler, rect.Height / scaler).ValidateSize();
	public static RectInt operator *(in RectInt rect, in Point2 scaler)
		=> new RectInt(rect.X * scaler.X, rect.Y * scaler.Y, rect.Width * scaler.X, rect.Height * scaler.Y).ValidateSize();
	public static RectInt operator /(in RectInt rect, in Point2 scaler)
		=> new RectInt(rect.X / scaler.X, rect.Y / scaler.Y, rect.Width / scaler.X, rect.Height / scaler.Y).ValidateSize();
	public static Rect operator *(in RectInt rect, float scaler)
		=> new Rect(rect.X * scaler, rect.Y * scaler, rect.Width * scaler, rect.Height * scaler).ValidateSize();
	public static Rect operator /(in RectInt rect, float scaler)
		=> new Rect(rect.X / scaler, rect.Y / scaler, rect.Width / scaler, rect.Height / scaler).ValidateSize();
	public static Rect operator *(in RectInt rect, in Vector2 scaler)
		=> new Rect(rect.X * scaler.X, rect.Y * scaler.Y, rect.Width * scaler.X, rect.Height * scaler.Y).ValidateSize();
	public static Rect operator /(in RectInt rect, in Vector2 scaler)
		=> new Rect(rect.X / scaler.X, rect.Y / scaler.Y, rect.Width / scaler.X, rect.Height / scaler.Y).ValidateSize();
	public static RectInt operator *(in RectInt rect, Facing flipX) => flipX == Facing.Right ? rect : rect.ScaleX(-1);
	public static RectInt operator *(in RectInt rect, Cardinal rotation) => rect.Rotate(rotation);
}
