using RuDataAPI.Extensions;
using RuDataAPI.Extensions.Mapping;
using RuDataAPI.Extensions.Ratings;

namespace FiduciaryCalculator
{
    public class BondBuilder
    {        
        private readonly List<CreditRatingUS> _usrs;                    // list of us ratings
        private readonly List<CreditRatingRU> _rurs;                    // list of ru ratings
        
        private IEnumerable<Tenor> _cpns;                               // list of coupon tenors
        private IEnumerable<Tenor> _amrts;                               // list of amortizations
        private Tenor _put;                                             // PUT offer
               
        private DateTime _start;                                        // date of placement
        private DateTime _end;                                          // redemption date
        
        private int _nper;                                              // number of coupon periods
        private int _cpery;                                             // number of coupons per year
        private Tenor _clen;                                            // coupon length in days
        private CouponType _ctype;                                      // coupon type

        private double _face;                                           // initial face value
        private bool _nonstd;                                           // non-standard
        private bool _isamort;                                          // amortization

        public BondBuilder()
        {
            _usrs = new();
            _rurs = new();
            _cpns = Enumerable.Empty<Tenor>();
            _amrts = Enumerable.Empty<Tenor>();
            _start = DateTime.Now.Date.AddDays(-1);
            _face = 1000;
            _clen = 182;
            _nper = 2;
            _cpery = 2;
            _ctype = CouponType.Constant;
            _nonstd = false;
            _isamort = false;
        }
            
        
        public static BondBuilder New() 
            => new();

        public BondBuilder WithStartDate(DateTime date)
        {
            _start = date.Date;
            return this;
        }

        public BondBuilder WithMaturityDate(DateTime date) 
        {
            _end = date.Date;
            return this;
        }

        public BondBuilder WithCouponType(CouponType type)
        {
            _ctype = type;
            return this;
        }     

        public BondBuilder WithCouponLength(Tenor tenor)
        {
            _clen = tenor;
            return this;
        }
        
        public BondBuilder WithCouponLength(params Tenor[] coupons)
        {
            _cpns=coupons;
            return this;
        }


        public BondBuilder WithNumberOfCoupons(int n)
        {
            _nper = n;
            return this;
        }

        public BondBuilder WithPutOffers(Tenor put)
        {
            // var d = double.Parse(input)
            // int days = (int)(d * 365)
            // parse to Tenor

            _put = put;
            return this;
        }

        public BondBuilder WithAmortizations(params Tenor[] amrts)
        {
            _amrts = amrts;
            return this;
        }

        public BondBuilder WithRating(CreditRatingUS rating)
        {
            _usrs.Add(rating);
            return this;
        }

        public BondBuilder WithRating(CreditRatingRU rating)
        {
           _rurs.Add(rating);
            return this;
        }

        public BondBuilder WithRatings(CreditRatingUS[] ratings)
        {
            _usrs.AddRange(ratings);
            return this;
        }

        public BondBuilder WithRatings(CreditRatingRU[] ratings)
        {
            _rurs.AddRange(ratings);
            return this;
        }

        public BondBuilder WithFaceValue(double face)
        {
            _face = face;
            return this;
        }

        public InstrumentInfo Build()
        {
            var bond = new InstrumentInfo();

            if (!_cpns.Any())
                _cpns = Enumerable.Repeat(_clen, _nper);

            var coupons = GenerateCoupons();
            var offers = GeneratePutOffers();
            var redemptions = GenerateRedemption();

            _end = _start;
            foreach (var coupon in coupons)
                _end += coupon.PeriodLength;

            bond.PlacementDate = _start;
            bond.MaturityDate = _end;
            if (_rurs.Any()) bond.RatingAggregated = new CreditRatingAggregated(_rurs.ToArray());
            bond.Currency = "RUB";
            bond.CouponType = _ctype;
            bond.InitialFaceValue = _face;

            bond.Flows = coupons
                .Concat(offers)
                .Concat(redemptions)
                .OrderBy(fl => fl.EndDate)
                .ThenBy(fl => fl.PaymentType)
                .AsEnumerable();

            return bond;
        }

        private IEnumerable<InstrumentFlow> GenerateCoupons()
        {
            DateTime start = _start;
            foreach (var len in _cpns)
            {
                var fl = new InstrumentFlow
                {
                    PeriodLength = len,
                    StartDate = start,
                    EndDate = start + len,
                    PaymentType = FlowType.CPN
                };
                start = fl.EndDate;
                yield return fl;
            }
        }

        private IEnumerable<InstrumentFlow> GeneratePutOffers()
        {
            int clen = _clen.Days; // coupon length
            int putlen = _put.Days; // put offer length

            if (_put == 0) yield break;

            if (putlen > clen)
            {
                int n = putlen / clen;
                int l = putlen / n;
                Tenor trueTenor = new (n * l);
                yield return new InstrumentFlow
                {
                    PeriodLength = trueTenor,
                    StartDate = _start,
                    EndDate = _start + trueTenor,
                    Payment = _face,
                    Rate = 1.0,
                    PaymentType = FlowType.PUT
                };
            }
            else
            {
                yield return new InstrumentFlow
                {
                    PeriodLength = _put,
                    StartDate = _start,
                    EndDate = _start + _put,
                    Payment = _face,
                    Rate = 1.0,
                    PaymentType = FlowType.PUT
                };
            }
        }

        private IEnumerable<InstrumentFlow> GenerateRedemption()
        {
            int clen = _clen.Days; // coupon length

            if (!_amrts.Any())
            {
                yield return new InstrumentFlow
                {
                    PeriodLength = (_end - _start).Days,
                    StartDate = _start,
                    EndDate = _end,
                    Payment = _face,
                    Rate = 1.0,
                    PaymentType = FlowType.MTY
                };
                yield break;
            }

            double amrtPayment = _face / _amrts.Count();
            double amrtRate = amrtPayment / _face;

            foreach (var a in _amrts)
            {
                int amrtlen = a.Days; // amrt length
                if (amrtlen > clen)
                {
                    int n = amrtlen / clen;
                    int l = amrtlen / n;
                    Tenor trueTenor = new(n * l);
                    yield return new InstrumentFlow
                    {
                        PeriodLength = trueTenor,
                        StartDate = _start,
                        EndDate = _start + trueTenor,
                        Payment = amrtPayment,
                        Rate = amrtRate,
                        PaymentType = FlowType.AMRT
                    };
                }
                else
                {
                    yield return new InstrumentFlow
                    {
                        PeriodLength = _put,
                        StartDate = _start,
                        EndDate = _start + _put,
                        Payment = amrtPayment,
                        Rate = amrtRate,
                        PaymentType = FlowType.AMRT
                    };
                }
            }
        }
    }




}
