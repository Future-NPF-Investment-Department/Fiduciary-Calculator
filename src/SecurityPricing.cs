
namespace FiduciaryCalculator
{
    /// <summary>
    ///     Represents set of pricing metrics.
    /// </summary>
    public readonly struct SecurityPricing
    {
        /// <summary>
        ///     Current security price.
        /// </summary>
        public double PriceCurrent { get; init; }

        /// <summary>
        ///     Current security price in percent of notional.
        /// </summary>
        public double PricePctCurrent { get; init; }

        /// <summary>
        ///     Current security duration.
        /// </summary>
        public double DurationCurrent { get; init; }

        /// <summary>
        ///     Security duration at the moment of initial offering.
        /// </summary>
        public double DurationAtOffering { get; init; }

        /// <summary>
        ///     Security yield to maturity / to offer
        /// </summary>
        public double YtmCurrent { get; init; }

        /// <summary>
        ///     Security yield to maturity (to offer) at the moment of initial offering.
        /// </summary>
        public double YtmAtOffering { get; init; }

        /// <summary>
        ///     Current G-Spread.
        /// </summary>
        public double GspreadCurrent { get; init; }

        /// <summary>
        ///     G-Spread at the moment of initial offering.
        /// </summary>
        public double GspreadAtOffering { get; init; }        

        /// <summary>
        ///     Current Z-Spread.
        /// </summary>
        public double ZspreadCurrent { get; init; }

        /// <summary>
        ///     Z-Spread at the moment of initial offering.
        /// </summary>
        public double ZspreadAtOffering { get; init; }
        
        /// <summary>
        ///     Trading volume for the past 10 business days.
        /// </summary>
        public double TradeVolume { get; init; }
    }
}
