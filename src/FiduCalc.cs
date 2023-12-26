#pragma warning disable IDE1006 // Naming Styles

using Efir.DataHub.Models.Models.Bond;
using RuDataAPI;
using RuDataAPI.Extensions;
using RuDataAPI.Extensions.Mapping;
using ConsoleTables;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace FiduciaryCalculator
{
    /// <summary>
    ///     Fiduciary calculator. Provides static methods to find comparables to a bond, calculate yield, z-spread for a bond, etc.
    /// </summary>
    public static class FiduCalс
    {
        private static readonly EfirClient _efir = null!;
        private const FlowType CPN = FlowType.CPN;
        private const FlowType MTY = FlowType.MTY;
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
        ///     Calculates bond price for particular ISIN. 
        /// </summary>
        /// <param name="isin">Security ISIN.</param>
        /// <param name="pricedate">Date of pricing. If null bond price is calculated as of today.</param>
        /// <param name="ytm">Yield-to-maturity. If not specified gcurve rates will be used for discounting.</param>
        /// <returns>Bond price</returns>
        public static async Task<double> CalculateBondPriceAsync(string isin, DateTime? pricedate = null, double? ytm = null)
        {
            await ConnectEfirAsync();
            InstrumentInfo sec = await _efir.ExGetInstrumentInfoAsync(isin);
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
        public static async Task<double> CalculateBondPriceAsync(InstrumentInfo bond, DateTime? pricedate = null, double? ytm = null)
        {
            if (bond.Flows is null)
                throw new Exception("No bond schedule provided.");

            DateTime date = pricedate ?? DateTime.Now;
            List<double> dfs = new();

            if (ytm is null)
            {
                await ConnectEfirAsync();

            }




            //foreach(var flow in bond.Flows)
            //{
            //    //if (e.PaymentType != CPN && e.PaymentType != MTY) continue;                   // take only coupons and notional payments
            //    double ttm = (flow.EndDate - date).Days / 365.0;                                // calculate time-to-maturity
            //    if (ttm < 0) continue;                                                          // go next if ttm is negative
            //    double dr = ytm ?? await _efir.ExCalculateGcurveForDateAsync(date, ttm);        // get discount rate
            //    double df = flow.Payment / Math.Pow((1 + dr), ttm);                             // calculate discounted flow
            //    dfs.Add(df);
            //}

            return dfs.Sum();
        }

        /// <summary>
        ///     Calculates bond price for provided <see cref="EfirSecurity"/> using Z-Spread. 
        /// </summary>
        /// <param name="bond">Efir security (bond).</param>
        /// <param name="zspread">Z-Spread value.</param>
        /// <param name="pricedate">Date of pricing. If null bond price is calculated as of today.</param>
        /// <exception cref="Exception"> is thrown if bond's <see cref="EfirSecurity.EventsSchedule"/> is null.</exception>
        /// <returns>Bond price</returns>
        public static async Task<double> CalculateBondPriceAsync(InstrumentInfo bond, double zspread, DateTime? pricedate = null)
        {
            if (bond.Flows is null)
                throw new Exception("No bond schedule provided.");

            DateTime date = pricedate ?? DateTime.Now;
            List<double> dfs = new();

            await ConnectEfirAsync();

            foreach (var e in bond.Flows)
            {
                //if (e.PaymentType != CPN && e.PaymentType != MTY) continue;                         // take only coupons and notional payments
                double ttm = (e.EndDate - date).Days / 365.0;                                // calculate time-to-maturity
                if (ttm < 0) continue;                                                              // go next if ttm is negative
                double dr = await _efir.ExCalculateGcurveForDateAsync(date, ttm) + zspread / 10000;   // get discount rate
                double df = e.Payment / Math.Pow((1 + dr), ttm);                                    // calculate discounted flow
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
        /// <returns>Bond's YTM value.</returns>
        public static async Task<double> CalculateBondYtmAsync(string isin, DateTime? pricedate = null, double? price = null)
        {
            await ConnectEfirAsync();
            InstrumentInfo sec = await _efir.ExGetInstrumentInfoAsync(isin);
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
        /// <returns>Bond's YTM value.</returns>
        public static async Task<double> CalculateBondYtmAsync(InstrumentInfo bond, DateTime? pricedate = null, double? price = null)
        {
            if (price is null) await ConnectEfirAsync();
            double targetPrice = price ?? await CalculateBondPriceAsync(bond, pricedate, null);

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

        /// <summary>
        ///     Calculates bond duration at date.
        /// </summary>
        /// <param name="isin">Bond's ISIN code.</param>
        /// <param name="pricedate">Date of pricing. If null bond's duration is calculated as of today.</param>
        /// <param name="price">Bond's target price that is used to calculate duration. If null theretical bond's price is used obtained using g-curve for pricedate.</param>
        /// <returns>Bond's duration value.</returns>
        public static async Task<double> CalculateBondDurationAsync(string isin, DateTime? pricedate = null, double? price = null)
        {
            await ConnectEfirAsync();
            InstrumentInfo sec = await _efir.ExGetInstrumentInfoAsync(isin);
            return await CalculateBondDurationAsync(sec, pricedate, price);
        }

        /// <summary>
        ///     Calculates bond duration at date.
        /// </summary>
        /// <param name="bond"><see cref="EfirSecurity"/> that represents a bond.</param>
        /// <param name="pricedate">Date of pricing. If null bond's duration is calculated as of today.</param>
        /// <param name="price">Bond's target price that is used to calculate duration. If null theretical bond's price is used obtained using g-curve for pricedate.</param>
        /// <returns>Bond's duration value.</returns>
        public static async Task<double> CalculateBondDurationAsync(InstrumentInfo bond, DateTime? pricedate = null, double? price = null)
        {
            if (price is null) await ConnectEfirAsync();
            double targetPrice = price ?? await CalculateBondPriceAsync(bond, pricedate, null);
            
            if (bond.Flows is null)
                throw new Exception("No bond schedule provided.");

            DateTime date = pricedate ?? DateTime.Now;
            List<double> dfs = new();

            foreach (var e in bond.Flows)
            {
                if (e.PaymentType != CPN && e.PaymentType != MTY) continue;                 // take only coupons and notional payments
                double ttm = (e.EndDate - date).Days / 365.0;                               // calculate time-to-maturity
                if (ttm < 0) continue;                                                      // go next if ttm is negative
                double dr = await _efir.ExCalculateGcurveForDateAsync(date, ttm);             // get discount rate
                double df = e.Payment / Math.Pow((1 + dr), ttm) * ttm;                      // calculate discounted flow
                dfs.Add(df);
            }

            return dfs.Sum() / targetPrice;
        }

        /// <summary>
        ///     Calculates G-Spread for a bond at date.
        /// </summary>
        /// <param name="isin">Bond's ISIN code.</param>
        /// <param name="pricedate">Date of pricing. If null bond's duration is calculated as of today.</param>
        /// <param name="ytm">Known in advance bond's YTM that is used to calculate G-Spread. If null theretical bond's YTM is calculated </param>
        /// <returns>Bond's G-Spread value.</returns>
        public static async Task<double> CalculateGSpreadAsync(string isin, DateTime? pricedate = null, double? ytm = null)
        {
            await ConnectEfirAsync();
            InstrumentInfo sec = await _efir.ExGetInstrumentInfoAsync(isin);
            return await CalculateGSpreadAsync(sec, pricedate, ytm);
        }

        /// <summary>
        ///     Calculates G-Spread for a bond at date.
        /// </summary>
        /// <param name="bond"><see cref="EfirSecurity"/> that represents a bond.</param>
        /// <param name="pricedate">Date of pricing. If null bond's duration is calculated as of today.</param>
        /// <param name="ytm">Known in advance bond's YTM that is used to calculate G-Spread. If null theretical bond's YTM is calculated.</param>
        /// <returns>Bond's G-Spread value.</returns>
        public static async Task<double> CalculateGSpreadAsync(InstrumentInfo bond, DateTime? pricedate = null, double? ytm = null)
        {
            pricedate ??= DateTime.Now;
            double price = await CalculateBondPriceAsync(bond, pricedate, ytm);
            ytm ??= await CalculateBondYtmAsync(bond, pricedate, price);
            double dur = await CalculateBondDurationAsync(bond, pricedate, price);
            await ConnectEfirAsync();
            double gcurve = await _efir.ExCalculateGcurveForDateAsync(pricedate!.Value, dur);
            return (ytm!.Value - gcurve) * 10000;
        }

        /// <summary>
        ///     Calculates Z-Spread for a bond at date.
        /// </summary>
        /// <param name="isin">Bond's ISIN code.</param>
        /// <param name="pricedate">Date of pricing. If null bond's duration is calculated as of today.</param>
        /// <param name="price">Known in advance bond's price that is used to calculate G-Spread. If null theretical bond's price is calculated and used to calculate Z-Spread.</param>
        /// <returns>Bond's Z-Spread value.</returns>
        public static async Task<double> CalculateZSpreadAsync(string isin, DateTime? pricedate = null, double? price = null)
        {
            await ConnectEfirAsync();
            InstrumentInfo sec = await _efir.ExGetInstrumentInfoAsync(isin);
            return await CalculateGSpreadAsync(sec, pricedate, price);
        }

        /// <summary>
        ///     Calculates Z-Spread for a bond at date.
        /// </summary>
        /// <param name="bond"><see cref="EfirSecurity"/> that represents a bond.</param>
        /// <param name="pricedate">Date of pricing. If null bond's duration is calculated as of today.</param>
        /// <param name="price">Known in advance bond's price that is used to calculate G-Spread. If null theretical bond's price is calculated and used to calculate Z-Spread.</param>
        /// <returns>Bond's Z-Spread value.</returns>
        public static async Task<double> CalculateZSpreadAsync(InstrumentInfo bond, DateTime? pricedate = null, double? price = null)
        {
            if (price is null) await ConnectEfirAsync();
            double targetPrice = price ?? await CalculateBondPriceAsync(bond, pricedate, null);

            double x0 = -100.0, x1 = 500.0, x2 = -1;
            while (Math.Abs(x0 - x2) > 0.1)
            {
                double fx0 = await CalculateBondPriceAsync(bond, x0, pricedate);
                double fx1 = await CalculateBondPriceAsync(bond, x1, pricedate);
                x2 = x1 - (fx1 - targetPrice) * (x1 - x0) / (fx1 - fx0);
                x0 = x1;
                x1 = x2;
            }
            return x2;
        }


        
        public static async Task<List<(InstrumentInfo, SecurityPricing)>> GetAnalogs(EfirSecQueryDetails query)
        {
            await ConnectEfirAsync();
            var secs = await _efir.ExGetBondsAlike(query);
            var date = new DateTime(2023, 12, 22);
            var retval = new List<(InstrumentInfo, SecurityPricing)>(secs.Length);

            foreach (var sec in secs)
            {
                // skip if security does not have records for flows or trade history
                if (sec.TradeHistory is null || sec.Flows is null) continue;

                // mkt price
                var lastTradeRecord = sec.TradeHistory.Last();
                var price = lastTradeRecord.Close / 100.0 * lastTradeRecord.FaceValue;  // what if close or face == 0 ????
                if (price == 0 || price is double.NaN) throw new Exception($"Bad market data for {sec.Isin}");
                
                // vol, ytm, etc..
                var vol = sec.TradeHistory.Select(th => th.Volume).Average();                
                var duration = await CalculateBondDurationAsync(sec, date, price);
                var ytm = await CalculateBondYtmAsync(sec, date, price);
                var gspread = await CalculateGSpreadAsync(sec, date, ytm);
                var zspread = await CalculateZSpreadAsync(sec, date, price);

                var pricing = new SecurityPricing() { Price = price, TradeVolume = vol, Duration = duration, Ytm = ytm, Gspread = gspread, Zspread = zspread };
                retval.Add((sec, pricing));
            }

            return retval;  
        }


        //public static async Task ShowAnalogs(EfirSecQueryDetails query)
        //{
        //    await ConnectEfirAsync();
        //    var analogs = await _efir.ExFindAnalogsAsync(query);
        //    var isins = analogs.Select(a => a.Isin!).ToArray();

        //    var allflows = await _efir.GetEventsCalendarAsync(isins);


        //    var svod = new ConsoleTable("ISIN", "Issue", "Placement", "Maturity", "Ratiting BIG3", "Raiting RU", "Mkt. Price", "Th. Price", "YTM", "Duration", "G-Spread", "Z-Spread");

        //    for (int i = 0; i < isins.Length; i++)
        //    {
        //        analogs[i].EventsSchedule = GetFlowsForPricing(allflows, analogs[i].Isin!, out bool isBad);
        //        if (isBad) continue;


        //        var eod = await _efir.EndOfDay(analogs[i].Isin, new DateTime(2023, 12, 22));
        //        if (eod.last is null || eod.facevalue is null) continue;
                

        //        var price = (double)(eod.last / 100m * eod.facevalue) + ((double?)eod.accruedint ?? default);   // <---------------------------------------------  НАДО ДОБАВИТЬ НКД

        //        Console.WriteLine($"{analogs[i].Isin}\t{analogs[i].ShortName}");


        //        var ytm = await CalculateBondYtmAsync(analogs[i], new DateTime(2023, 12, 22), price);
        //        var zspread = await CalculateZSpreadAsync(analogs[i], new DateTime(2023, 12, 22), price); // analogs[i].PlacementDate, 1000
        //        var gspread = await CalculateGSpreadAsync(analogs[i], new DateTime(2023, 12, 22), ytm);
        //        var duration = await CalculateBondDurationAsync(analogs[i], new DateTime(2023, 12, 22), price);
        //        var tprice = await CalculateBondPriceAsync(analogs[i], new DateTime(2023, 12, 22), null);
                
        //        var table = new ConsoleTable("Type", "Start", "End", "Rate", "Flow");
        //        foreach ( var e in analogs[i].EventsSchedule!)
        //            table.AddRow(e.PaymentType, e.StartDate?.ToShortDateString(), e.EndDate?.ToShortDateString(), e.Rate, e.Payment);


        //        table.Write(Format.Minimal);
        //        Console.WriteLine();


        //        //svod.Configure(r => r.NumberAlignment = Alignment.Right);
        //        svod.AddRow(analogs[i].Isin!, eod.secname_e, analogs[i].PlacementDate?.ToShortDateString(),
        //            analogs[i].MaturityDate?.ToShortDateString(), analogs[i].RatingAggregated.ToShortStringBig3(), analogs[i].RatingAggregated.ToShortStringRu(), price, string.Format("{0:0.00}", tprice), string.Format("{0:0.00%}", ytm), 
        //            string.Format("{0:0.00}", duration), string.Format("{0:0}", gspread), string.Format("{0:0}", zspread));
        //    }

        //    svod.Configure(r => r.NumberAlignment = Alignment.Right).Write(Format.Minimal);
            
        //}


        //private static string[] CheckBadIsins(List<SecurityEvent> timetable)
        //{
        //    return timetable.Where(t => t.EndDate is null).Select(t => t.Isin).ToArray();
        //}

        //private static List<SecurityEvent> GetFlowsForPricing(TimeTableV2Fields[] events, string isin, out bool isBad)
        //{
        //    var flows = events
        //        .Select(e => new SecurityEvent(e))
        //        .Where(fl => fl.Isin == isin)
        //        .Where(e => e.PaymentType is not SecurityFlow.CALL)
        //        .Where(e => e.PaymentType is not SecurityFlow.CONV)
        //        .Where(e => e.PaymentType is not SecurityFlow.DIV)                
        //        .ToList();

        //    isBad = flows.Any(f => f.EndDate is null);

        //    DateTime? lastNonZeroCouponDate = flows
        //        .Where(e => e.PaymentType is SecurityFlow.CPN)
        //        .Where(e => e.Payment != 0)
        //        .Max(e => e.EndDate);

        //    foreach (var flow in flows)
        //    {
        //        if (flow.PaymentType is SecurityFlow.PUT && flow.EndDate < lastNonZeroCouponDate)
        //        {
        //            flow.Rate = default;
        //            flow.Payment = default;
        //        }

        //        if (flow.EndDate > lastNonZeroCouponDate)
        //        {
        //            flow.Rate = default;
        //            flow.Payment = default;
        //        }
        //    }
        //    return flows;
        //}


        


        /// <summary>
        ///     Connects to Efir Server if it is not connected.
        /// </summary>
        private static async Task ConnectEfirAsync()
        {
            if (!_efir.IsLoggedIn)            
                await _efir.LoginAsync();            
        }



        private static IEnumerable<double> GetDiscountedFlows(IEnumerable<InstrumentFlow> flows, DateTime date, double ytm)
        {
            const FlowType PUT = FlowType.PUT;
            bool offerAlreadyPassed = default;

            foreach (var flow in flows)
            {
                if (offerAlreadyPassed) continue;
                if (flow.PaymentType == PUT) offerAlreadyPassed = true;

                double ttm = (flow.EndDate - date).Days / 365.0;                                // calculate time-to-maturity
                if (ttm < 0) continue;                                                          // go next if ttm is negative
                yield return flow.Payment / Math.Pow((1 + ytm), ttm);                           // calculate discounted flow
            }
        }


        private static IEnumerable<double> GetDiscountedFlows(IEnumerable<InstrumentFlow> flows, DateTime date, Func<double, double> GcurveRateProvider)
        {
            const FlowType PUT = FlowType.PUT;
            bool offerAlreadyPassed = default;

            foreach (var flow in flows)
            {
                if (offerAlreadyPassed) continue;
                if (flow.PaymentType == PUT) offerAlreadyPassed = true;

                double ttm = (flow.EndDate - date).Days / 365.0;                                // calculate time-to-maturity
                if (ttm < 0) continue;                                                          // go next if ttm is negative
                double r = GcurveRateProvider(ttm);                                             // get risk-free discount rate from G-Curve
                yield return flow.Payment / Math.Pow((1 + r), ttm);                             // calculate discounted flow
            }
        }
    }
}
