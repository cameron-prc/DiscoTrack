namespace DiscoTrack.Common;

public sealed class Either<TLeft, TRight>
{
	private readonly TLeft? _left;
	private readonly TRight? _right;

	private Either(TLeft? left, TRight? right, bool isRight)
	{
		_left = left;
		_right = right;
		IsRight = isRight;
	}

	public bool IsRight { get; }

	public bool IsLeft
	{
		get { return !IsRight; }
	}

	public TLeft GetLeft
	{
		get
		{
			if (IsRight)
			{
				throw new InvalidOperationException("Left value requested on right valued Either");
			}
			return _left!;
		}
	}

	public TRight GetRight
	{
		get
		{
			if (IsLeft)
			{
				throw new InvalidOperationException("Right value requested on left valued Either");
			}
			return _right!;
		}
	}

	public static Either<TLeft, TRight> Left(TLeft left)
	{
		return new Either<TLeft, TRight>(left, default, false);
	}

	public static Either<TLeft, TRight> Right(TRight right)
	{
		return new Either<TLeft, TRight>(default, right, true);
	}

	public TResult Match<TResult>(Func<TLeft, TResult> left, Func<TRight, TResult> right)
	{
		if (IsRight)
		{
			return right(_right!);
		}
		return left(_left!);
	}

	public void Match(Action<TLeft> left, Action<TRight> right)
	{
		if (IsRight)
		{
			right(_right!);
			return;
		}
		left(_left!);
	}

	public static implicit operator Either<TLeft, TRight>(TLeft left)
	{
		return Left(left);
	}

	public static implicit operator Either<TLeft, TRight>(TRight right)
	{
		return Right(right);
	}
}
