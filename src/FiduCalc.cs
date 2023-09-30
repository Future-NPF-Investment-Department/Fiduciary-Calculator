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

        static FiduCalс()
        {
            if (!File.Exists("EfirCredentials.json")) throw new FileNotFoundException("Cannot find file: EfirCredentials.json", "EfirCredentials.json");
            var creds = EfirClient.GetCredentialsFromFile("EfirCredentials.json");
            _efir = new EfirClient(creds);
        }

        /// <summary>
        ///     Calculates bond price for particular ISIN. 
        /// </summary>
        /// <param name="isin">Security ISIN.</param>
        /// <param name="pricedate">Date of pricing. If null bond price is calculated as of today.</param>
        /// <param name="ytm">Yield-to-maturity. If not specified gcurve rates will be used for discounting.</param>
        /// <returns>Bond price</returns>
        public static async Task<double> CalculateBondPrice(string isin, DateTime? pricedate = null, double? ytm = null)
        {
            await ConnectEfir();
            EfirSecurity sec = await _efir.GetEfirSecurityAsync(isin, true);
            return await CalculateBondPrice(sec, pricedate, ytm);
        }

        /// <summary>
        ///     Calculates bond price for provided <see cref="EfirSecurity"/>. 
        /// </summary>
        /// <param name="bond">Efir security (bond).</param>
        /// <param name="pricedate">Date of pricing. If null bond price is calculated as of today.</param>
        /// <param name="ytm">Yield-to-maturity. If not specified gcurve rates will be used for discounting.</param>
        /// <exception cref="Exception"> is thrown if bond's <see cref="EfirSecurity.EventsSchedule"/> is null.</exception>
        /// <returns>Bond price</returns>
        public static async Task<double> CalculateBondPrice(EfirSecurity bond, DateTime? pricedate = null, double? ytm = null)
        {
            const EventType CPN = EventType.CPN;
            const EventType MTY = EventType.MTY;

            if (bond.EventsSchedule is null)
                throw new Exception("No bond schedule provided.");

            DateTime date = pricedate ?? DateTime.Now;
            List<double> dfs = new();

            if (ytm is not null)
                await ConnectEfir();

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
        ///     Connects to Efir Server if it is not connected.
        /// </summary>
        private static async Task ConnectEfir()
        {
            if (!_efir.IsLoggedIn)            
                await _efir.LoginAsync();            
        }
    }
}
