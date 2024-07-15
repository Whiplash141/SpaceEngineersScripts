
/// <summary>
/// Solves for the real roots of quadratic in the form: 0 = a*x^2 + b*x + c.
/// </summary>
/// <param name="a">Coefficient of the x^2 term</param>
/// <param name="b">Coefficient of the x term</param>
/// <param name="c">Constant term</param>
/// <param name="roots">List of real solutions</param>
/// <param name="epsilon">Small floating point epsilon to prevent division by zero.</param>
/// <returns>True if at least one real solution exists.</returns>
public static bool RealQuadraticRoots(double a, double b, double c, List<double> roots, double epsilon = 1e-12)
{
    roots.Clear();

    // Linear
    if (Math.Abs(a) < epsilon)
    {
        if (Math.Abs(b) < epsilon)
        {
            return false;
        }
        roots.Add(-c / b);
        return true;
    }

    // Quadratic
    double d = b * b - 4.0 * a * c;
    if (d < 0 || Math.Abs(a) < epsilon)
    {
        return false;
    }
    
    double sqrtD = Math.Sqrt(d);
    double inv2a = 1.0 / (2.0 * a);
    
    double x1 = (-b + sqrtD) * inv2a;
    double x2 = (-b - sqrtD) * inv2a;
    roots.Add(x1);
    roots.Add(x2);
    roots.Sort();

    return true;
}

/// <summary>
/// Solves for the real roots of a cubic in the form: 0 = a*x^3 + b*x^2 + c*x + d.
/// </summary>
/// <remarks>
/// See: https://quarticequations.com/Cubic.pdf
/// </remarks>
/// <param name="a">Coefficient of the x^3 term</param>
/// <param name="b">Coefficient of the x^2 term</param>
/// <param name="c">Coefficient of the x term</param>
/// <param name="d">Constant term</param>
/// <param name="roots">List of real solutions</param>
/// <param name="epsilon">Small floating point epsilon to prevent division by zero.</param>
/// <returns>True if at least one real solution exists.</returns>
public static bool RealCubicRoots(double a, double b, double c, double d, List<double> roots, double epsilon = 1e-12)
{
    roots.Clear();

    if (Math.Abs(a) < epsilon)
    {
        return RealQuadraticRoots(b, c, d, roots, epsilon);
    }

    double a2 = b / a;
    double a1 = c / a;
    double a0 = d / a;

    double oneThird = 1.0 / 3.0;
    double a2Sq = a2 * a2;
    double q = (a1 / 3.0 - a2Sq / 9.0);
    double r = (a1 * a2 - 3 * a0) / 6.0 - (a2Sq * a2) / 27.0;

    double s = (r * r) + (q * q * q);

    if (s > 0)
    {
        double A = Math.Pow(Math.Abs(r) + Math.Sqrt(s), 1.0 / 3.0);
        double t1 = (r >= 0) ? (A - q / A) : (q / A - A);
        double z1 = t1 - (a2 / 3);

        roots.Add(z1);
    }
    else
    {
        double theta = (q < 0) ? (Math.Acos(r / Math.Sqrt(-q * q * q))) : 0;
        double phi1 = theta * oneThird;
        double phi2 = phi1 - 2 * Math.PI * oneThird;
        double phi3 = phi1 + 2 * Math.PI * oneThird;

        double twoRootQ = 2 * Math.Sqrt(-q);
        double a2Over3 = a2 * oneThird;

        double z1 = twoRootQ * Math.Cos(phi1) - a2Over3;
        double z2 = twoRootQ * Math.Cos(phi2) - a2Over3;
        double z3 = twoRootQ * Math.Cos(phi3) - a2Over3;

        roots.Add(z3);
        roots.Add(z2);
        roots.Add(z1);
    }

    return true;
}

/// <summary>
/// Solves for the real roots of a quartic in the form: 0 = a*x^4 + b*x^3 + c*x^2 + d*x + e.
/// </summary>
/// <remarks>
/// See: https://quarticequations.com/Quartic2.pdf - Modified Ferrari's Method
/// </remarks>
/// <param name="a">Coefficient of the x^4 term</param>
/// <param name="b">Coefficient of the x^3 term</param>
/// <param name="c">Coefficient of the x^2 term</param>
/// <param name="d">Coefficient of the x term</param>
/// <param name="e">Constant term</param>
/// <param name="roots">List of real solutions</param>
/// <param name="epsilon">Small floating point epsilon to prevent division by zero.</param>
/// <returns>True if at least one real solution exists.</returns>
public static bool RealQuarticRoots(double a, double b, double c, double d, double e, List<double> roots, double epsilon = 1e-12)
{
    roots.Clear();

    if (Math.Abs(a) < epsilon)
    {
        return RealCubicRoots(b, c, d, e, roots, epsilon);
    }

    double a3 = b / a;
    double a2 = c / a;
    double a1 = d / a;
    double a0 = e / a;

    double C = a3 / 4.0;
    double CSq = C * C;
    double b2 = a2 - 6 * CSq;
    double b1 = a1 - 2 * a2 * C + 8 * C * CSq;
    double b0 = a0 - a1 * C + a2 * CSq - 3 * CSq * CSq;

    RealCubicRoots(1, b2, b2 * b2 / 4.0 - b0, -b1 * b1 / 8.0, roots, epsilon);
    double m = 0;
    if (roots.Count > 0)
    {
        double max = roots[roots.Count - 1];
        if (max > 0)
        {
            m = max;
        }
    }
    roots.Clear();

    double E = (b1 > 0) ? 1 : -1;
    double R = E * Math.Sqrt(m * m + b2 * m + b2 * b2 / 4.0 - b0);
    double rootMOver2 = Math.Sqrt(m / 2.0);

    double temp = -(m + b2) / 2.0;

    double radicand = temp - R;
    if (radicand >= 0)
    {
        double result = Math.Sqrt(radicand);
        roots.Add(+rootMOver2 - C + result);
        roots.Add(+rootMOver2 - C - result);
    }

    radicand = temp + R;
    if (radicand >= 0)
    {
        double result = Math.Sqrt(radicand);
        roots.Add(-rootMOver2 - C + result);
        roots.Add(-rootMOver2 - C - result);
    }

    roots.Sort();

    return roots.Count > 0;
}
