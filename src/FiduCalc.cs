using RuDataAPI;
using RuDataAPI.Extensions;

namespace FiduciaryCalculator
{
    /// <summary>
    ///     Fiduciary calculator. Provides static methods to find comparables to a bond, calculate yield, z-spread for a bond, etc.
    /// </summary>
    public static class FiduCalс
    {
        private static readonly EfirClient _efir = null!;
        private const EventType CPN = EventType.CPN;
        private const EventType MTY = EventType.MTY;
        static FiduCalс()
        {
            if (!File.Exists("EfirCredentials.json")) throw new FileNotFoundException("Cannot find file: EfirCredentials.json", "EfirCredentials.json");
            var creds = EfirClient.GetCredentialsFromFile("EfirCredentials.json");
            _efir = new EfirClient(creds);
        }

        /// <summary>
        ///     EfirClient instance that is used to connect to EFIR server.
        /// </summary>
        public static EfirClient EfirClient => _efir;

        /// <summary>
        ///     Shows Bond information.
        /// </summary>
        /// <param name="isin">Bond ISIN.</param>
        public static async Task ShowSecurityInfo(string isin)
        {
            await ConnectEfirAsync();
            var sec = await _efir.GetEfirSecurityAsync(isin, true);
            ShowSecurityInfo(sec);
        }

        /// <summary>
        ///     Shows Bond information.
        /// </summary>
        /// <param name="bond">Efir security (bond)</param>
        public static void ShowSecurityInfo(EfirSecurity bond)
        {
            Console.WriteLine(bond.AssetClass);
            Console.WriteLine(bond.ShortName);
            Console.WriteLine(bond.Isin);
            Console.WriteLine(bond.IssuerName);
            Console.WriteLine(bond.IssueSector);
            Console.WriteLine();
            Console.WriteLine($"Placement date: {bond.PlacementDate!.Value.ToShortDateString()}");
            Console.WriteLine($"Maturity date: {bond.MaturityDate!.Value.ToShortDateString()}");
            Console.WriteLine();
            Console.WriteLine($"Coupon type: {bond.CouponType}");
            Console.WriteLine($"Coupon period type: {bond.CouponPeriodType}");
            Console.WriteLine($"First coupon start: {bond.FirstCouponStartDate}");
            Console.WriteLine($"Coupon reference: {bond.CouponReferenceRateName}");
            Console.WriteLine();
            Console.WriteLine("FLOWS:");
            foreach(var f in bond.EventsSchedule)
                Console.WriteLine($"{f.PaymentType} - {f.StartDate.Value.ToShortDateString()} - {f.EndDate.Value.ToShortDateString()} - {f.PeriodLength} - {f.Rate} - {f.Payment}");
        }

        /// <summary>
        ///     Calculates bond price for particular ISIN. 
        /// </summary>
        /// <param name="isin">Security ISIN.</param>
        /// <param name="pricedate">Date of pricing. If null bond price is calculated as of today.</param>
        /// <param name="ytm">Yield-to-maturity. If not specified gcurve rates will be used for discounting.</param>
        /// <returns>Bond price</returns>
        public static async Task<double> CalculateBondPriceAsync(string isin, DateTime? pricedate = null, double? ytm = null)
        {
            await ConnectEfirAsync();
            EfirSecurity sec = await _efir.GetEfirSecurityAsync(isin, true);
            return await CalculateBondPriceAsync(sec, pricedate, ytm);
        }

        /// <summary>
        ///     Calculates bond price for provided <see cref="EfirSecurity"/>. 
        /// </summary>
        /// <param name="bond">Efir security (bond).</param>
        /// <param name="pricedate">Date of pricing. If null bond price is calculated as of today.</param>
        /// <param name="ytm">Yield-to-maturity. If not specified gcurve rates will be used for discounting.</param>
        /// <exception cref="Exception"> is thrown if bond's <see cref="EfirSecurity.EventsSchedule"/> is null.</exception>
        /// <returns>Bond price</returns>
        public static async Task<double> CalculateBondPriceAsync(EfirSecurity bond, DateTime? pricedate = null, double? ytm = null)
        {
            if (bond.EventsSchedule is null)
                throw new Exception("No bond schedule provided.");

            DateTime date = pricedate ?? DateTime.Now;
            List<double> dfs = new();

            if (ytm is null)
                await ConnectEfirAsync();

            foreach(var e in bond.EventsSchedule)
            {
                if (e.PaymentType != CPN && e.PaymentType != MTY) continue;                 // take only coupons and notional payments
                double ttm = (e.EndDate!.Value - date).Days / 365.0;                        // calculate time-to-maturity
                if (ttm < 0) continue;                                                      // go next if ttm is negative
                double dr = ytm ?? await _efir.CalculateGcurveForDateAsync(date, ttm);      // get discount rate
                double df = e.Payment / Math.Pow((1 + dr), ttm);                            // calculate discounted flow
                dfs.Add(df);
            }

            return dfs.Sum();
        }

        /// <summary>
        ///     Calculates bond's yield to maturity.
        /// </summary>
        /// <remarks>
        ///     To calculate YTM Secant method is used. For more details see <see href="https://en.wikipedia.org/wiki/Secant_method">
        ///         https://en.wikipedia.org/wiki/Secant_method
        ///         </see>.
        /// </remarks>
        /// <param name="isin">Bond ISIN.</param>
        /// <param name="pricedate">Date of pricing. If null bond's YTM is calculated as of today.</param>
        /// <param name="price">Bond's target price that is used to calculate YTM. If null theretical bond's price is used obtained using g-curve for pricedate.</param>
        /// <returns></returns>
        public static async Task<double> CalculateBondYtmAsync(string isin, DateTime? pricedate = null, double? price = null)
        {
            await ConnectEfirAsync();
            EfirSecurity sec = await _efir.GetEfirSecurityAsync(isin, true);
            return await CalculateBondYtmAsync(sec, pricedate, price);
        }

        /// <summary>
        ///     Calculates bond's yield to maturity.
        /// </summary>
        /// <remarks>
        ///     To calculate YTM Secant method is used. For more details see <see href="https://en.wikipedia.org/wiki/Secant_method">
        ///         https://en.wikipedia.org/wiki/Secant_method
        ///         </see>.
        /// </remarks>
        /// <param name="bond">Efir security (bond).</param>
        /// <param name="pricedate">Date of pricing. If null bond's YTM is calculated as of today.</param>
        /// <param name="price">Bond's target price that is used to calculate YTM. If null theretical bond's price is used obtained using g-curve for pricedate.</param>
        /// <returns></returns>
        public static async Task<double> CalculateBondYtmAsync(EfirSecurity bond, DateTime? pricedate = null, double? price = null)
        {
            if (price is null) await ConnectEfirAsync();
            double targetPrice = price ?? await CalculateBondPriceAsync(bond, pricedate);

            double x0 = .05, x1 = .25, x2 = -1.0;
            while (Math.Abs(x0 - x2) > 1e-10)
            {   
                double fx0 = await CalculateBondPriceAsync(bond, null, x0);
                double fx1 = await CalculateBondPriceAsync(bond, null, x1);
                x2 = x1 - (fx1 - targetPrice) * (x1 - x0) / (fx1 - fx0);
                x0 = x1;
                x1 = x2;
            }
            return x2;
        }

        public static async Task<double> CalculateBondDurationAsync(string isin, DateTime? pricedate = null, double? price = null)
        {
            await ConnectEfirAsync();
            EfirSecurity sec = await _efir.GetEfirSecurityAsync(isin, true);
            return await CalculateBondDurationAsync(sec, pricedate, price);
        }

        public static async Task<double> CalculateBondDurationAsync(EfirSecurity bond, DateTime? pricedate = null, double? price = null)
        {
            if (price is null) await ConnectEfirAsync();
            double targetPrice = price ?? await CalculateBondPriceAsync(bond, pricedate, null);
            
            if (bond.EventsSchedule is null)
                throw new Exception("No bond schedule provided.");

            DateTime date = pricedate ?? DateTime.Now;
            List<double> dfs = new();

            foreach (var e in bond.EventsSchedule)
            {
                if (e.PaymentType != CPN && e.PaymentType != MTY) continue;                 // take only coupons and notional payments
                double ttm = (e.EndDate!.Value - date).Days / 365.0;                        // calculate time-to-maturity
                if (ttm < 0) continue;                                                      // go next if ttm is negative
                double dr = await _efir.CalculateGcurveForDateAsync(date, ttm);             // get discount rate
                double df = e.Payment / Math.Pow((1 + dr), ttm) * ttm;                      // calculate discounted flow
                dfs.Add(df);
            }

            return dfs.Sum() / targetPrice;
        }


        public static async Task<double> CalculateGSpreadAsync(string isin, DateTime? pricedate = null, double? ytm = null)
        {
            await ConnectEfirAsync();
            EfirSecurity sec = await _efir.GetEfirSecurityAsync(isin, true);
            return await CalculateGSpreadAsync(sec, pricedate, ytm);
        }

        public static async Task<int> CalculateGSpreadAsync(EfirSecurity bond, DateTime? pricedate = null, double? ytm = null)
        {
            pricedate ??= DateTime.Now;
            double price = await CalculateBondPriceAsync(bond, pricedate, ytm);
            ytm ??= await CalculateBondYtmAsync(bond, pricedate, price);
            double dur = await CalculateBondDurationAsync(bond, pricedate, price);
            await ConnectEfirAsync();
            double gcurve = await _efir.CalculateGcurveForDateAsync(pricedate!.Value, dur);
            return (int)((ytm!.Value - gcurve) * 10000);
        }

        /// <summary>
        ///     Connects to Efir Server if it is not connected.
        /// </summary>
        private static async Task ConnectEfirAsync()
        {
            if (!_efir.IsLoggedIn)            
                await _efir.LoginAsync();            
        }
    }
}
