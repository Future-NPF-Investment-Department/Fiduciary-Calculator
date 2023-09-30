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

        public static async Task<double> CalculateBondPrice(string isin, DateTime? pricedate = null, double? ytm = null)
        {
            const EventType CPN = EventType.CPN;
            const EventType MTY = EventType.MTY;
            DateTime date = pricedate ?? DateTime.Now;

            await ConnectEfir();
            EfirSecurity sec = await _efir.GetEfirSecurityAsync(isin, true);
            List<SecurityEvent> events = sec.EventsSchedule!;
            double[] dfs = new double[events.Count];

            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].PaymentType != CPN && events[i].PaymentType != MTY) continue;         // take only coupons and notional payments
                double ttm = (events[i].EndDate!.Value - date).Days / 365.0;                        // calculate time-to-maturity
                double dr = (ttm < .0) ? 0.0 : await _efir.CalculateGcurveForDateAsync(date, ttm);  // get discount rate
                double pow = (ttm < .0) ? 1.0 : ttm;                                                // calculate power
                double df = events[i].Payment / Math.Pow((1 + dr), pow);                            // calculate discounted flow
                dfs[i] = df;
            }

            return dfs.Sum();
        }

        private static async Task ConnectEfir()
        {
            if (!_efir.IsLoggedIn)            
                await _efir.LoginAsync();            
        }
    }
}
