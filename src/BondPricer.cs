#pragma warning disable IDE1006 // Naming Styles

using RuDataAPI;
using RuDataAPI.Extensions;
using RuDataAPI.Extensions.Mapping;

namespace FiduciaryCalculator
{
    /// <summary>
    ///     Fiduciary calculator. Provides static methods to find comparables to a bond, calculate yield, z-spread for a bond, etc.
    /// </summary>
    public class BondPricer
    {
        private readonly EfirClient _efir;
        private const FlowType PUT = FlowType.PUT;
        
        public BondPricer(EfirClient client)
        {
            _efir = client;
        }

        /// <summary>
        ///     EfirClient instance that is used to connect to EFIR server.
        /// </summary>
        public EfirClient EfirClient => _efir;

        /// <summary>
        ///     Calculates bond price using secant method. 
        /// </summary>
        /// <param name="bond">Efir security (bond).</param>
        /// <param name="date">Date of pricing.</param>
        /// <param name="ytm">Yield-to-maturity. If not specified gcurve rates will be used for discounting.</param>
        /// <exception cref="Exception"> is thrown if bond's <see cref="InstrumentInfo.Flows"/> is null.</exception>
        /// <returns>Bond price.</returns>
        public static double CalculatePrice(InstrumentInfo bond, DateTime date, double ytm)
        {
            if (bond.Flows is null)
                throw new Exception("No bond schedule provided.");    
            var dfs = GetDiscountedFlows(bond.Flows, date, ytm);         
            return dfs.Sum();
        }

        /// <summary>
        ///     Calculates bond price using provided Z-Spread calue. Calculation performed using secant method.
        /// </summary>
        /// <param name="bond">Efir security (bond).</param>
        /// <param name="zspread">Z-Spread value.</param>
        /// <param name="pricedate">Date of pricing.</param>
        /// <exception cref="Exception"> is thrown if bond's <see cref="InstrumentInfo.Flows"/> is null.</exception>
        /// <returns>Bond price</returns>
        public static double CalculatePrice(InstrumentInfo bond, YieldCurve curve, double zspread = .0)
        {
            if (bond.Flows is null)
                throw new Exception("No bond schedule provided.");
            var dfs = GetDiscountedFlows(bond.Flows, curve, zspread).ToArray();
            return dfs.Sum();
        }

        /// <summary>
        ///     Calculates bond's yield to maturity using secant method.
        /// </summary>
        /// <param name="bond">Efir security (bond).</param>
        /// <param name="date">Date of pricing. If null bond's YTM is calculated as of today.</param>
        /// <param name="price">Bond's target price that is used to calculate YTM.</param>
        /// <returns>Bond's YTM value.</returns>
        public static double CalculateYtm(InstrumentInfo bond, DateTime date, double price)
        {
            double x0 = -.75, 
                   x1 = .25, 
                   x2 = -1.0;

            while (Math.Abs(x0 - x2) > 1e-10)
            {   
                double fx0 = CalculatePrice(bond, date, x0);
                double fx1 = CalculatePrice(bond, date, x1);
                x2 = x1 - (fx1 - price) * (x1 - x0) / (fx1 - fx0);
                x0 = x1;
                x1 = x2;
            }
            return x2;
        }

        /// <summary>
        ///     Calculates bond duration at date using secant method.
        /// </summary>
        /// <param name="bond"><see cref="InstrumentInfo"/> that represents a bond.</param>
        /// <param name="curve">Zero curve that used for pricing.</param>
        /// <param name="price">Bond's target price that is used to calculate duration. If null theretical bond's price is used obtained using g-curve for pricedate.</param>
        /// <returns>Bond's duration value.</returns>
        public static double CalculateDuration(InstrumentInfo bond, YieldCurve curve, double price)
        {            
            if (bond.Flows is null)
                throw new Exception("No bond schedule provided.");
            var dfs = GetWeightedTenors(bond.Flows, curve);
            return dfs.Sum() / price;
        }

        /// <summary>
        ///     Calculates bond's G-Spread at date using secant method.
        /// </summary>
        /// <param name="bond"><see cref="InstrumentInfo"/> that represents a bond.</param>
        /// <param name="curve">Zero curve that used for pricing.</param>
        /// <param name="ytm">Known in advance bond's YTM that is used to calculate G-Spread. If null theretical bond's YTM is calculated.</param>
        /// <returns>Bond's G-Spread value.</returns>
        public static double CalculateGSpread(InstrumentInfo bond, YieldCurve curve, double ytm, double price)
        {
            double dur = CalculateDuration(bond, curve, price);
            double gcurve = curve.GetValueForTenor(dur);
            return (ytm - gcurve) * 10000;
        }

        /// <summary>
        ///     Calculates bond's Z-Spread at date using secant method.
        /// </summary>
        /// <param name="bond"><see cref="InstrumentInfo"/> that represents a bond.</param>
        /// <param name="curve">Zero curve that used for pricing.</param>
        /// <param name="price">Known in advance bond's price that is used to calculate G-Spread. If null theretical bond's price is calculated and used to calculate Z-Spread.</param>
        /// <returns>Bond's Z-Spread value.</returns>
        public static double CalculateZSpread(InstrumentInfo bond, YieldCurve curve, double price)
        {
            double x0 = -7500.0, x1 = 500.0, x2 = -1;
            while (Math.Abs(x0 - x2) > 0.1)
            {
                double fx0 = CalculatePrice(bond, curve, x0);
                double fx1 = CalculatePrice(bond, curve, x1);
                x2 = x1 - (fx1 - price) * (x1 - x0) / (fx1 - fx0);
                x0 = x1;
                x1 = x2;
            }
            return x2;
        }       

        /// <summary>
        ///     Searches for security analogs that fulfill provided criteria.
        /// </summary>
        /// <param name="query">Search criteria.</param>
        /// <returns>List of analogs.</returns>
        public async Task<List<(InstrumentInfo, SecurityPricing, bool)>> GetAnalogs(EfirSecQueryDetails query)
        {
            var date = new DateTime(2024, 4, 4);
            var retval = new List<(InstrumentInfo, SecurityPricing, bool)>();            

            await ConnectEfirAsync();
            var secs = (await _efir.ExSearchBonds(query))
                .Where(sec => sec.TradeHistory != null)
                .Where(sec => sec.Flows != null);
                                                                                                                
            var tasks = secs
                .Select(sec => sec.PlacementDate)
                .Distinct()
                .Append(date)
                .Select(async t => await _efir.GetGCurve(t, CurveProvider.MOEX))
                .ToArray();

            var gcurves = (await Task.WhenAll(tasks))
                .ToDictionary(gc => gc.Date);

            foreach (var sec in secs)
            {
                bool badQuality = false;

                var last_trade = sec.TradeHistory!.Last();
                var price_init = sec.InitialFaceValue;
                var price_curr = last_trade.Close / 100.0 * last_trade.FaceValue;

                if (price_curr == 0 || price_curr is double.NaN)
                    badQuality = true;

                price_curr += last_trade.AccruedInterest;

                var vol_curr = sec.TradeHistory!.Average(th => th.Volume);

                // metric calc at the moment of offering
                var ytm_init = CalculateYtm(sec, sec.PlacementDate, price_init);
                var dur_init = CalculateDuration(sec, gcurves[sec.PlacementDate], price_init);
                var gsprd_init = CalculateGSpread(sec, gcurves[sec.PlacementDate], ytm_init, price_init);
                var zsprd_init = CalculateZSpread(sec, gcurves[sec.PlacementDate], price_init);

                // metric calc at current moment
                var ytm_curr = CalculateYtm(sec, date, price_curr);
                var dur_curr = CalculateDuration(sec, gcurves[date], price_curr);
                var gsprd_curr = CalculateGSpread(sec, gcurves[date], ytm_curr, price_curr);
                var zsprd_curr = CalculateZSpread(sec, gcurves[date], price_curr);

                if (vol_curr < 300_000 || ytm_curr > 1 || ytm_init > 1)
                    badQuality = true;                

                if (dur_curr    is double.NaN 
                 || ytm_curr    is double.NaN 
                 || gsprd_curr  is double.NaN 
                 || zsprd_curr  is double.NaN
                 || dur_init    is double.NaN
                 || ytm_init    is double.NaN
                 || gsprd_init  is double.NaN
                 || zsprd_init  is double.NaN) badQuality = true; 

                var pricing = new SecurityPricing() { 
                    PriceCurrent = price_init, 
                    PricePctCurrent = last_trade.Close,
                    TradeVolume = vol_curr, 
                    DurationCurrent = dur_curr, 
                    DurationAtOffering = dur_init,
                    YtmCurrent = ytm_curr, 
                    YtmAtOffering = ytm_init,
                    GspreadCurrent = gsprd_curr, 
                    GspreadAtOffering = gsprd_init,
                    ZspreadCurrent = zsprd_curr, 
                    ZspreadAtOffering = zsprd_init
                };
                retval.Add((sec, pricing, badQuality));
            }
            return retval;  
        } 

        /// <summary>
        ///     Generates flows discounted at specified rate (YTM) to specified date.
        /// </summary>
        /// <param name="flows">Security flows.</param>
        /// <param name="date">Date of discounting.</param>
        /// <param name="rate">Discounting rate.</param>
        private static IEnumerable<double> GetDiscountedFlows(IEnumerable<InstrumentFlow> flows, DateTime date, double rate)
        {
            DateTime offerDate = DateTime.MaxValue;
            foreach (var flow in flows)
            {
                if (flow.EndDate < date) continue;
                if (flow.EndDate > offerDate) yield break;
                if (flow.PaymentType == PUT) offerDate = flow.EndDate;

                double ttm = (flow.EndDate - date).Days / 365.0;                               
                double df = flow.Payment / Math.Pow((1 + rate), ttm);
                yield return df;                          
            }
        }

        /// <summary>
        ///     Generates flows discounted at specified zero-curve and Z-Spread value.
        /// </summary>
        /// <param name="flows">Security flows.</param>
        /// <param name="curve">Zero curve that used for discounting.</param>
        /// <param name="zspread">Z-Spread value.</param>
        private static IEnumerable<double> GetDiscountedFlows(IEnumerable<InstrumentFlow> flows, YieldCurve curve, double zspread)
        {
            DateTime offerDate = DateTime.MaxValue;
            foreach (var flow in flows)
            {
                if (flow.EndDate < curve.Date) continue;
                if (flow.EndDate > offerDate) yield break;
                if (flow.PaymentType == PUT) offerDate = flow.EndDate;

                double ttm = (flow.EndDate - curve.Date).Days / 365.0;                               
                double r = curve.GetValueForTenor(ttm) + zspread / 10000;                                            
                double df = flow.Payment / Math.Pow((1 + r), ttm);
                yield return df;                             
            }
        }

        /// <summary>
        ///     Generates weigted by time flows discounted at specified zero-curve.
        /// </summary>
        /// <param name="flows">Security flows.</param>
        /// <param name="curve">Zero curve that used for discounting.</param>
        private static IEnumerable<double> GetWeightedTenors(IEnumerable<InstrumentFlow> flows, YieldCurve curve)
        {
            DateTime offerDate = DateTime.MaxValue;
            foreach (var flow in flows)
            {
                if (flow.EndDate < curve.Date) continue;
                if (flow.EndDate > offerDate) yield break;
                if (flow.PaymentType == PUT) offerDate = flow.EndDate;

                double ttm = (flow.EndDate - curve.Date).Days / 365.0;                         
                double r = curve.GetValueForTenor(ttm);                                        
                yield return flow.Payment / Math.Pow((1 + r), ttm) * ttm;                      
            }
        }

        /// <summary>
        ///     Calculates bond price using secant method. 
        /// </summary>
        /// <param name="bond">Efir security (bond).</param>
        /// <param name="date">Date of pricing.</param>
        /// <param name="ytm">Yield-to-maturity. If not specified gcurve rates will be used for discounting.</param>
        /// <exception cref="Exception"> is thrown if bond's <see cref="InstrumentInfo.Flows"/> is null.</exception>
        /// <returns>Bond price.</returns>
        public async Task<double> CalculatePriceAsync(string isin, DateTime date, double ytm)
        {
            await ConnectEfirAsync();
            var sec = await _efir.ExGetInstrumentInfo(isin);
            return CalculatePrice(sec, date, ytm);
        }

        /// <summary>
        ///     Calculates bond price using provided Z-Spread calue. Calculation performed using secant method.
        /// </summary>
        /// <param name="bond">Efir security (bond).</param>
        /// <param name="zspread">Z-Spread value.</param>
        /// <param name="pricedate">Date of pricing.</param>
        /// <exception cref="Exception"> is thrown if bond's <see cref="InstrumentInfo.Flows"/> is null.</exception>
        /// <returns>Bond price</returns>
        public async Task<double> CalculatePriceAsync(string isin, YieldCurve curve, double zspread = .0)
        {
            await ConnectEfirAsync();
            var sec = await _efir.ExGetInstrumentInfo(isin);
            return CalculatePrice(sec, curve, zspread);
        }

        /// <summary>
        ///     Calculates bond's yield to maturity using secant method.
        /// </summary>
        /// <param name="bond">Efir security (bond).</param>
        /// <param name="date">Date of pricing. If null bond's YTM is calculated as of today.</param>
        /// <param name="price">Bond's target price that is used to calculate YTM.</param>
        /// <returns>Bond's YTM value.</returns>
        public async Task<double> CalculateYtmAsync(string isin, DateTime date, double price)
        {
            await ConnectEfirAsync();
            var sec = await _efir.ExGetInstrumentInfo(isin);
            return CalculateYtm(sec, date, price);
        }

        /// <summary>
        ///     Calculates bond duration at date using secant method.
        /// </summary>
        /// <param name="bond"><see cref="InstrumentInfo"/> that represents a bond.</param>
        /// <param name="curve">Zero curve that used for pricing.</param>
        /// <param name="price">Bond's target price that is used to calculate duration. If null theretical bond's price is used obtained using g-curve for pricedate.</param>
        /// <returns>Bond's duration value.</returns>
        public async Task<double> CalculateDurationAsync(string isin, YieldCurve curve, double price)
        {
            await ConnectEfirAsync();
            var sec = await _efir.ExGetInstrumentInfo(isin);
            return CalculateDuration(sec, curve, price);
        }

        /// <summary>
        ///     Calculates bond's G-Spread at date using secant method.
        /// </summary>
        /// <param name="bond"><see cref="InstrumentInfo"/> that represents a bond.</param>
        /// <param name="curve">Zero curve that used for pricing.</param>
        /// <param name="ytm">Known in advance bond's YTM that is used to calculate G-Spread. If null theretical bond's YTM is calculated.</param>
        /// <returns>Bond's G-Spread value.</returns>
        public async Task<double> CalculateGspreadAsync(string isin, YieldCurve curve, double ytm, double price)
        {
            await ConnectEfirAsync();
            var sec = await _efir.ExGetInstrumentInfo(isin);
            return CalculateGSpread(sec, curve, ytm, price);
        }

        /// <summary>
        ///     Calculates bond's Z-Spread at date using secant method.
        /// </summary>
        /// <param name="bond"><see cref="InstrumentInfo"/> that represents a bond.</param>
        /// <param name="curve">Zero curve that used for pricing.</param>
        /// <param name="price">Known in advance bond's price that is used to calculate G-Spread. If null theretical bond's price is calculated and used to calculate Z-Spread.</param>
        /// <returns>Bond's Z-Spread value.</returns>
        public async Task<double> CalculateZspreadAsync(string isin, YieldCurve curve, double price)
        {
            await ConnectEfirAsync();
            var sec = await _efir.ExGetInstrumentInfo(isin);
            return CalculateZSpread(sec, curve, price);
        }

        /// <summary>
        ///     Connects to Efir Server if it is not connected.
        /// </summary>
        private async Task ConnectEfirAsync()
        {
            if (!_efir.IsLoggedIn)
                await _efir.LoginAsync();
        }
    }
}
