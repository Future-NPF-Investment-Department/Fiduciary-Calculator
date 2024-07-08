using RuDataAPI.Extensions;

namespace FiduciaryCalculator
{
    /// <summary>
    ///     Represents pricing function used by Secant method to find equilibrium value of parameter specified.
    /// </summary>
    /// <param name="parameter">Should be one of these: z-spread, yeild-to-maturity, coupon rate</param>
    /// <returns>Bond price equal to zero.</returns>
    public delegate double BondPricingFunction(double parameter);




    public abstract class BondPricing
    {

        private protected BondPricingResults _results;

        public static VanillaBondPricing NewVanillaPricing(Discounting discounting, double? price, double? ytm, double? zspread, double? couponRate)
            => new(discounting, price, ytm, zspread, couponRate);

        //public static VanillaBondPricing NewVanillaPricing(Discounting discounting, double? price, double? ytm, double? zspread)
        //    => new(discounting, price, ytm, zspread);

        public static double Solve(BondPricingFunction func, double x0, double x1)
        {            
            for (int i = 0; i < 250; i++)
            {
                double f0 = func(x0);
                double f1 = func(x1);
                double x2 = x1 - f1 * (x1 - x0) / (f1 - f0);
                (x0, x1) = (x1, x2);
                if (Math.Abs(x0 - x2) <= 1e-10)
                    return x2;
            }
            return double.NaN;
        }

        public abstract BondPricing Price();
    }
}
