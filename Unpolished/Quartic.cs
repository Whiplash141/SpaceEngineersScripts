public struct ComplexNumber
{
    public readonly double Real;
    public readonly double Imaginary;
    private const double Epsilon = 1e-9;

    public static readonly ComplexNumber Zero = new ComplexNumber(0, 0);

    public ComplexNumber(double real, double imaginary)
    {
        Real = real;
        Imaginary = imaginary;
    }

    #region Negation
    public static ComplexNumber operator -(ComplexNumber s1)
    {
        return new ComplexNumber(-s1.Real, -s1.Imaginary);
    }
    #endregion

    #region Addition
    public static ComplexNumber operator +(ComplexNumber s1, ComplexNumber s2)
    {
        return new ComplexNumber(s1.Real + s2.Real, s1.Imaginary + s2.Imaginary);
    }

    public static ComplexNumber operator +(ComplexNumber s1, double num)
    {
        return s1 + new ComplexNumber(num, 0);
    }

    public static ComplexNumber operator +(double num, ComplexNumber s1)
    {
        return new ComplexNumber(num, 0) + s1;
    }
    #endregion

    #region Subtraction
    public static ComplexNumber operator -(ComplexNumber s1, ComplexNumber s2)
    {
        return new ComplexNumber(s1.Real - s2.Real, s1.Imaginary - s2.Imaginary);
    }

    public static ComplexNumber operator -(ComplexNumber s1, double num)
    {
        return s1 - new ComplexNumber(num, 0);
    }

    public static ComplexNumber operator -(double num, ComplexNumber s1)
    {
        return new ComplexNumber(num, 0) - s1;
    }

    #endregion

    #region Multiplacation
    public static ComplexNumber operator *(ComplexNumber s1, ComplexNumber s2)
    {
        return new ComplexNumber(s1.Real * s2.Real - s1.Imaginary * s2.Imaginary, s1.Imaginary * s2.Real + s1.Real * s2.Imaginary);
    }

    public static ComplexNumber operator *(double num, ComplexNumber s1)
    {
        return new ComplexNumber(num, 0) * s1;
    }

    public static ComplexNumber operator *(ComplexNumber s1, double num)
    {
        return s1 * new ComplexNumber(num, 0);
    }
    #endregion

    #region Division
    public static ComplexNumber operator /(ComplexNumber s1, ComplexNumber s2)
    {
        double denom = s2.MagnitudeSquared();
        ComplexNumber reciprocal = new ComplexNumber(s2.Real / denom, -s2.Imaginary / denom);
        return s1 * reciprocal;
    }

    public static ComplexNumber operator /(double num, ComplexNumber s1)
    {
        return new ComplexNumber(num, 0) / s1;
    }

    public static ComplexNumber operator /(ComplexNumber s1, double num)
    {
        return s1 / new ComplexNumber(num, 0);
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
        return Math.Abs(s1.Real - s2.Real) < Epsilon && Math.Abs(s1.Imaginary - s2.Imaginary) < Epsilon;
    }

    public static bool operator !=(ComplexNumber s1, ComplexNumber s2)
    {
        return Math.Abs(s1.Real - s2.Real) > Epsilon || Math.Abs(s1.Imaginary - s2.Imaginary) > Epsilon;
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

    public double MagnitudeSquared()
    {
        return Real * Real + Imaginary * Imaginary;
    }

    public double Magnitude()
    {
        return Math.Sqrt(MagnitudeSquared());
    }

    public bool IsReal
    {
        get
        {
            return Math.Abs(Imaginary) < Epsilon;
        }
    }

    public bool IsImaginary
    {
        get
        {
            return Math.Abs(Imaginary) > Epsilon && Math.Abs(Real) < Epsilon;
        }
    }

    public bool IsComplex
    {
        get
        {
            return Math.Abs(Imaginary) > Epsilon;
        }
    }
    #endregion

    #region Roots
    public static double? RealRoot(ComplexNumber s1, uint root)
    {
        for (uint ii = 0; ii < root; ++ii)
        {
            var num = Root(s1, root, ii);
            if (num.IsReal)
            {
                return num.Real;
            }
        }
        return null;
    }

    public static double? RealRoot(double num, uint root)
    {
        ComplexNumber s1 = new ComplexNumber(num, 0);
        return RealRoot(s1, root);
    }

    public static ComplexNumber PrincipalRoot(ComplexNumber s1, uint root)
    {
        ComplexNumber maxReal = new ComplexNumber(double.MinValue, 0);
        for (uint ii = 0; ii < root; ++ii)
        {
            var r = Root(s1, root, ii);
            if (r.Real > maxReal.Real)
            {
                maxReal = r;
            }
        }
        return maxReal;
    }

    public static ComplexNumber PrincipalRoot(double num, uint root)
    {
        ComplexNumber s1 = new ComplexNumber(num, 0);
        return PrincipalRoot(s1, root);
    }

    public static ComplexNumber Root(ComplexNumber s1, uint root, uint index = 0)
    {
        index = Math.Min(root - 1, index);

        double magnitude = s1.Magnitude();
        double angle = s1.Angle();
        double offset = index * 2 * Math.PI;
        return Math.Pow(magnitude, 1.0 / root) * new ComplexNumber(Math.Cos((angle + offset) / root), Math.Sin((angle + offset) / root));
    }

    public static ComplexNumber Root(double num, uint root, uint index = 0)
    {
        var s1 = new ComplexNumber(num, 0);
        return Root(s1, root, index);
    }

    public static ComplexNumber Sqrt(double num, uint index = 0)
    {
        return Root(num, 2, index);
    }
    #endregion

    #region Exponents
    public static ComplexNumber Pow(ComplexNumber s1, uint power)
    {
        double magnitude = s1.Magnitude();
        double angle = s1.Angle();
        return Math.Pow(magnitude, power) * new ComplexNumber(Math.Cos(power * angle), Math.Sin(power * angle));
    }

    public static ComplexNumber Square(ComplexNumber s1)
    {
        return s1 * s1;
    }

    #endregion

    public override string ToString()
    {
        string connector = Imaginary < 0 ? "-" : "+";
        return $"{Real} {connector} {Math.Abs(Imaginary)}i";
    }
}

public struct PolynomialRoots : IEnumerable<ComplexNumber>
{   
    const double Epsilon = 1e-12;
    ComplexNumber _root0;
    ComplexNumber _root1;
    ComplexNumber _root2;
    ComplexNumber _root3;

    int _writeIdx;

    public PolynomialRoots(byte _ = 0)
    {
        _root0 = _root1 = _root2 = _root3 = ComplexNumber.Zero;
        _writeIdx = 0;
    }

    public bool IsFull => _writeIdx == 4;

    public int Count => _writeIdx;

    public ComplexNumber this[int pos]
    {
        get
        {
            if (pos >= _writeIdx || pos < 0)
            {
                throw new Exception($"Index out of bounds: {nameof(pos)}");
            }
            switch (pos)
            {
                case 0: return _root0;
                case 1: return _root1;
                case 2: return _root2;
                default: return _root3;
            }
        }
        set
        {
            if (pos >= _writeIdx || pos < 0)
            {
                throw new Exception($"Index out of bounds: {nameof(pos)}");
            }
            switch (pos)
            {
                case 0: _root0 = value; break;
                case 1: _root1 = value; break;
                case 2: _root2 = value; break;
                default: _root3 = value; break;
            }
        }
    }

    public bool AddRoot(ComplexNumber s)
    {
        if (IsFull)
        {
            return false;
        }

        for (int pos = 0; pos < _writeIdx; ++pos)
        {
            if ((s - this[pos]).MagnitudeSquared() < Epsilon)
            {
                return false;
            }
        }

        this[_writeIdx++] = s;
        return true;
    }

    public IEnumerator<ComplexNumber> GetEnumerator()
    {
        for (int ii = 0; ii < Count; ++ii)
        {
            yield return this[ii];
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return (IEnumerator)GetEnumerator();
    }
}

public static class Quadratic
{
    const double Epsilon = 1e-12;

    /// <summary>
    /// Finds roots of a quadratic equation in the form: a*x^2 + b*x + c = 0.
    /// </summary>
    /// <returns>True if a solution exists</returns>
    public static bool FindRoots(double a, double b, double c, out PolynomialRoots roots, double epsilon = Epsilon)
    {
        roots = new PolynomialRoots();

        // Linear
        if (Math.Abs(a) < epsilon)
        {
            if (Math.Abs(b) < epsilon)
            {
                return false;
            }
            var x = new ComplexNumber(-c / b, 0);
            roots.AddRoot(x);
            return true;
        }

        // Quadratic
        double d = b * b - 4.0 * a * c;
        double inv2a = 1.0 / (2.0 * a);
        for (uint root = 0; root < 2; ++root)
        {
            roots.AddRoot((-b + ComplexNumber.Sqrt(d, root)) * inv2a);
        }
        return true;
    }
}

public static class Cubic
{
    const double Epsilon = 1e-12;

    /// <summary>
    /// Finds the roots of a cubic function in the form: a*x^3 + b*x^2 + c*x + d = 0.
    /// </summary>
    /// <see href="https://mathworld.wolfram.com/CubicFormula.html"/>
    /// <returns>True if solution exists</returns>
    public static bool FindRoots(double a, double b, double c, double d, out PolynomialRoots roots, double epsilon = Epsilon)
    {
        roots = new PolynomialRoots();
        if (Math.Abs(a) < 1e-9)
        {
            PolynomialRoots xSoln;
            if (!Quadratic.FindRoots(b,c,d, out xSoln, epsilon))
            {
                return false;
            }

            foreach (ComplexNumber x in xSoln)
            {
                roots.AddRoot(x);
            }
            return true;
        }

        double invA = 1.0 / a;
        double a2 = b * invA;
        double a1 = c * invA;
        double a0 = d * invA;

        double p = (3 * a1 - a2 * a2) / 3;
        double q = (9 * a1 * a2 - 27 * a0 - 2 * a2 * a2 * a2) / 27;

        // let x = t - a2/3
        // t^3 + p*t = q
        double a2over3 = a2 / 3.0;
        if (Math.Abs(p) < epsilon)
        {
            for (uint ii = 0; ii < 3; ++ii)
            {
                roots.AddRoot(ComplexNumber.Root(q, 3, ii) - a2over3);
            }
            return true;
        }
        else if (Math.Abs(q) < epsilon)
        {
            roots.AddRoot(ComplexNumber.Zero - a2over3);
            
            // t*(t^2+p) = 0
            for (uint ii = 0; ii < 2; ++ii)
            {
                roots.AddRoot(ComplexNumber.Root(-p, 2, ii) - a2over3);
            }
            return true;
        }
        
        PolynomialRoots uSoln;
        Quadratic.FindRoots(1, -q, -1.0 / 27.0 * p * p * p, out uSoln, epsilon);
        
        for (uint cubeIdx = 0; cubeIdx < 3; ++cubeIdx)
        {
            foreach (ComplexNumber u in uSoln)
            {
                var w = ComplexNumber.Root(u, 3, cubeIdx);
                ComplexNumber x = w - p / (3 * w) - a2over3;
                roots.AddRoot(x);
            }
        }
        return true;
    }
}

public static class Quartic
{
    const double Epsilon = 1e-12;


    /// <summary>
    /// Finds the roots of a quartic function in the form: a*x^4 + b*x^3 + c*x^2 + d*x + e = 0.
    /// </summary>
    /// <see href="https://mathworld.wolfram.com/QuarticFormula.html"/>
    /// <returns>True if solution exists</returns>
    public static bool FindRoots(double a, double b, double c, double d, double e, out PolynomialRoots roots, double epsilon = Epsilon)
    {
        roots = new PolynomialRoots();

        if (Math.Abs(a) < epsilon)
        {
            PolynomialRoots x;
            if (!Cubic.FindRoots(b,c,d,e, out x, epsilon))
            {
                return false;
            }

            foreach (ComplexNumber root in x)
            {
                roots.AddRoot(root);
            }
            return true;
        }

        double invA = 1.0 / a;
        double a3 = b * invA;
        double a2 = c * invA;
        double a1 = d * invA;
        double a0 = e * invA;

        // Resolvent cubic
        PolynomialRoots ySoln;
        Cubic.FindRoots(1, -a2, a1 * a3 - 4 * a0, 4 * a2 * a0 - a1 * a1 - a3 * a3 * a0, out ySoln, epsilon);

        double y1 = 0;
        foreach(ComplexNumber y in ySoln)
        {
            if (y.IsReal)
            {
                y1 = y.Real;
                break;
            }
        }

        ComplexNumber R = ComplexNumber.PrincipalRoot(0.25 * a3 * a3 - a2 + y1, 2);
        ComplexNumber D, E;
        if (R.MagnitudeSquared() > epsilon)
        {
            ComplexNumber RInv = 1.0 / R;
            ComplexNumber k1 = 0.75 * a3 * a3 - R * R - 2 * a2;
            ComplexNumber k2 = 0.25 * (4 * a3 * a2 - 8 * a1 - a3 * a3 * a3) * RInv;
            D = ComplexNumber.PrincipalRoot(k1 + k2, 2);
            E = ComplexNumber.PrincipalRoot(k1 - k2, 2);
        }
        else
        {
            double k1 = 0.75 * a3 * a3 - 2 * a2;
            ComplexNumber k2 = 2 * ComplexNumber.PrincipalRoot(y1 * y1 - 4 * a0, 2);
            D = ComplexNumber.PrincipalRoot(k1 + k2, 2);
            E = ComplexNumber.PrincipalRoot(k1 - k2, 2);
        }

        roots.AddRoot(-0.25*a3 + 0.5*R + 0.5*D);
        roots.AddRoot(-0.25*a3 + 0.5*R - 0.5*D);
        roots.AddRoot(-0.25*a3 - 0.5*R + 0.5*E);
        roots.AddRoot(-0.25*a3 - 0.5*R - 0.5*E);

        return true;
    }
}
