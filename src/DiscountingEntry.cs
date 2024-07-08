using RuDataAPI.Extensions;

namespace FiduciaryCalculator
{
    public class DiscountingEntry
    {
        public double FaceValue { get; set; }

        public DateTime Date { get; set; }

        public Tenor Tenor { get; set; }

        public double TimeToFlowDate { get; set; }

        public double? InterestRate { get; set; }

        public double? InterestValue { get; set; }

        public double AmortValue { get; set; }

        public double TotalValue { get; set; }

        public double DiscountRate { get; set; }

        public double Zspread { get; set; }

        public double DiscountFactor { get; set; }

        public double DiscountedValue { get; set; }
    }





}
