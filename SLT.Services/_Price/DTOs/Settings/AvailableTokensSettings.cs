namespace SLT.Services._Price.DTOs.Settings
{
    public class AvailableTokensSettings : List<AvailableTokenData>
    {

    }
     
    public class AvailableTokenData
    {
        private string _name;

        public string Name // always return upper case
        {
            get => _name?.ToUpper();
            set => _name = value;
        }

        public string Network { get; set; }
        public string Address { get; set; }
        public string PoolId { get; set; }
        public string PoolName { get; set; }
        public int PriceDecimalPlaces { get; set; }
        public int AmountDecimalPlaces { get; set; }
    }


}
