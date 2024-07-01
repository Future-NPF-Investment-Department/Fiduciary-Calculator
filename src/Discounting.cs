using RuDataAPI.Extensions;
using RuDataAPI.Extensions.Mapping;
using System.Collections;

namespace FiduciaryCalculator
{
    public sealed class Discounting : IEnumerable<DiscountingEntry>
    {
        private readonly List<DiscountingEntry> _entries;
        private readonly YieldCurve _curve;

        private double _face;
        private DateTime _date;

        private Discounting(IEnumerable<DiscountingEntry> entries, YieldCurve curve)
        {
            _entries = new();
            _entries.AddRange(entries);
            _curve = curve;
        }

        private Discounting(YieldCurve curve, double faceValue)
        {
            _entries = new();
            _date = curve.Date;
            _face = faceValue;
            _curve = curve;
        }

        public DiscountingEntry this[int i] => _entries[i];    
        public YieldCurve Curve => _curve;  
        public int Length => _entries.Count;

        public static Discounting New(YieldCurve curve, int ncpn, Tenor tenor, double initialFace)
        {
            var disc = new Discounting(curve, initialFace);
            for (int i = 0; i < ncpn - 1; i++)
                disc.AddEntry(tenor, .0, 0);
            disc.AddEntry(tenor, .0, initialFace);
            return disc;
        }

        public static Discounting New(YieldCurve curve, double initialFace)
            => new(curve, initialFace);

        public static Discounting FromFlows(IEnumerable<InstrumentFlow> flows, YieldCurve curve)
        {
            const FlowType CPN  = FlowType.CPN;
            const FlowType PUT  = FlowType.PUT;
            const FlowType CALL = FlowType.CALL;
            const FlowType AMRT = FlowType.AMRT;
            const FlowType MTY  = FlowType.MTY;

            // get all flows that occurs after pricing date
            var flowsIncoming = flows
                .Where(f => f.EndDate > curve.Date)
                .Where(f => f.PaymentType != CALL);

            // get starting face value
            double face = flowsIncoming
                .Where(f => f.PaymentType == AMRT || f.PaymentType == MTY)
                .Select(f => f.Payment)
                .Sum();

            // get next put date
            DateTime nearestPut = flowsIncoming
                .Where(f => f.PaymentType == PUT)
                .Select(f => f.EndDate)
                .OrderBy(d => d)
                .FirstOrDefault(DateTime.MaxValue);

            // get list of discounting entries
            var disc = flowsIncoming
                .Where(f => f.EndDate <= nearestPut)
                .GroupBy(f => f.EndDate)
                .Select(g =>
                {
                    // get tenor
                    Tenor t = g.Where(f => f.PaymentType == CPN)
                    .Select(f => f.PeriodLength)
                    .FirstOrDefault(new Tenor(0));

                    // get cpn rate
                    double cpnr = g.Where(f => f.PaymentType == CPN)
                    .Select(f => f.Rate)
                    .DefaultIfEmpty(0)
                    .Sum();

                    // get cpn pmt
                    double cpnv = g.Where(f => f.PaymentType == CPN)
                    .Select(f => f.Payment)
                    .DefaultIfEmpty(0)
                    .Sum();

                    // get amrt pmt
                    double amrt = g.Where(f => f.PaymentType != CPN)
                    .Select(f => f.Payment)
                    .DefaultIfEmpty(0)
                    .Sum();

                    // new DiscountingEntry
                    var de = new DiscountingEntry()
                    {
                        FaceValue = face,
                        Date = g.Key,
                        Tenor = t,
                        InterestRate = cpnr,
                        InterestValue = cpnv,
                        AmortValue = amrt,
                        TotalValue = cpnv + amrt
                    };

                    // adjusting face
                    face -= amrt;

                    // return DiscountingEntry
                    return de;
                });

            return new Discounting(disc.ToList(), curve);
        }

        public Discounting AddEntry(Tenor tenor, double rate, double facePmt)
        {
            _date += tenor;
            var cpnpmt = _face * rate * tenor.Years;
            var entry = new DiscountingEntry()
            {
                FaceValue = _face,
                Date = _date,
                Tenor = tenor,
                TimeToFlowDate = (_date - _curve.Date).Days / 365.0,
                InterestRate = rate,
                InterestValue = cpnpmt,
                AmortValue = facePmt,
                TotalValue = cpnpmt + facePmt
            };
            _face -= facePmt;
            _entries.Add(entry);
            return this; 
        }


        public IEnumerator<DiscountingEntry> GetEnumerator()
            => _entries.GetEnumerator();
        

        IEnumerator IEnumerable.GetEnumerator()        
            => GetEnumerator();

    }





}
