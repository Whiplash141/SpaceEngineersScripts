public struct ComplexNumber
{
    public readonly double Real;
    public readonly double Imaginary;
    private const double epsilon = 1e-9;

    public ComplexNumber(double real, double imaginary)
    {
        Real = real;
        Imaginary = imaginary;
    }

    #region Addition
    public static ComplexNumber operator +(ComplexNumber s1, double num)
    {
        return new ComplexNumber(s1.Real + num, s1.Imaginary);
    }

    public static ComplexNumber operator +(double num, ComplexNumber s1)
    {
        return new ComplexNumber(s1.Real + num, s1.Imaginary);
    }

    public static ComplexNumber operator +(ComplexNumber s1, ComplexNumber s2)
    {
        return new ComplexNumber(s1.Real + s2.Real, s1.Imaginary + s2.Imaginary);
    }
    #endregion

    #region Subtraction
    public static ComplexNumber operator -(ComplexNumber s1, double num)
    {
        return new ComplexNumber(s1.Real - num, s1.Imaginary);
    }

    public static ComplexNumber operator -(double num, ComplexNumber s1)
    {
        return new ComplexNumber(num - s1.Real, s1.Imaginary);
    }

    public static ComplexNumber operator -(ComplexNumber s1, ComplexNumber s2)
    {
        return new ComplexNumber(s1.Real - s2.Real, s1.Imaginary - s2.Imaginary);
    }
    #endregion

    #region Multiplacation
    public static ComplexNumber operator *(ComplexNumber s1, ComplexNumber s2)
    {
        return new ComplexNumber(s1.Real * s2.Real - s1.Imaginary * s2.Imaginary, s1.Imaginary * s2.Real + s1.Real * s2.Imaginary);
    }

    public static ComplexNumber operator *(double num, ComplexNumber s1)
    {
        return new ComplexNumber(num * s1.Real, num * s1.Imaginary);
    }

    public static ComplexNumber operator *(ComplexNumber s1, double num)
    {
        return new ComplexNumber(num * s1.Real, num * s1.Imaginary);
    }
    #endregion

    #region Division
    public static ComplexNumber operator /(ComplexNumber s1, ComplexNumber s2)
    {
        ComplexNumber numerator = s1 * s2.Conjugate();
        ComplexNumber demominator = s2 * s2.Conjugate(); //this will be real
        return new ComplexNumber(numerator.Real / demominator.Real, numerator.Imaginary / demominator.Real);
    }

    public static ComplexNumber operator /(double num, ComplexNumber s1)
    {
        ComplexNumber numerator = num * s1.Conjugate();
        ComplexNumber demominator = s1 * s1.Conjugate(); //this will be real
        return new ComplexNumber(numerator.Real / demominator.Real, numerator.Imaginary / demominator.Real);
    }

    public static ComplexNumber operator /(ComplexNumber s1, double num)
    {
        return new ComplexNumber(s1.Real / num, s1.Imaginary / num);
    }
    #endregion

    #region Equality
    public override bool Equals(Object s1)
    {
        return s1 is ComplexNumber && (ComplexNumber)s1 == new ComplexNumber(Real, Imaginary);
    }

    public override int GetHashCode()
    {
        return Real.GetHashCode() ^ Imaginary.GetHashCode();
    }

    public static bool operator ==(ComplexNumber s1, ComplexNumber s2)
    {
        return Math.Abs(s1.Real - s2.Real) < epsilon && Math.Abs(s1.Imaginary - s2.Imaginary) < epsilon;
    }

    public static bool operator !=(ComplexNumber s1, ComplexNumber s2)
    {
        return Math.Abs(s1.Real - s2.Real) > epsilon || Math.Abs(s1.Imaginary - s2.Imaginary) > epsilon;
    }
    #endregion

    #region Complex Number Properties
    public ComplexNumber Conjugate()
    {
        return new ComplexNumber(Real, -Imaginary);
    }

    public double Angle()
    {
        return Math.Atan2(Imaginary, Real);
    }

    public double Magnitude()
    {
        return Math.Sqrt(Real * Real + Imaginary * Imaginary);
    }
    #endregion

    #region Booleans
    public bool IsReal()
    {
        return Math.Abs(Imaginary) < epsilon;
    }

    public bool IsImaginary()
    {
        return Math.Abs(Imaginary) > epsilon && Math.Abs(Real) < epsilon;
    }

    public bool IsComplex()
    {
        return Math.Abs(Imaginary) > epsilon && Math.Abs(Real) > epsilon;
    }
    #endregion

    #region Exponents
    public static ComplexNumber Pow(ComplexNumber s1, double num)
    {
        double magnitude = s1.Magnitude();
        double angle = s1.Angle();
        return Math.Pow(magnitude, num) * new ComplexNumber(Math.Cos(num * angle), Math.Sin(num * angle));
    }

    public static ComplexNumber Pow(ComplexNumber s1, int num)
    {
        var result = new ComplexNumber(1, 0);

        if (num == 0)
            return result;

        if (num < 0)
        {
            for (int i = 1; i <= -num; i++)
            {
                result = result * s1;
            }

            return 1 / result;
        }

        for (int i = 1; i <= num; i++)
        {
            result = result * s1;
        }

        return result;
    }

    public static ComplexNumber Square(ComplexNumber s1)
    {
        return s1 * s1;
    }

    public static ComplexNumber Sqrt(double num)
    {
        if (num < 0)
            return new ComplexNumber(0, Math.Sqrt(-num));
        else
            return new ComplexNumber(Math.Sqrt(num), 0);
    }
    #endregion

    public override string ToString()
    {
        string connector = Imaginary < 0 ? "-" : "+";
        return $"{Real} {connector} {Math.Abs(Imaginary)}i";
    }
}