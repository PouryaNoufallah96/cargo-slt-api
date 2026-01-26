namespace Utilities.DTOs
{
    public class Pagination
    {
        private int _size = 25;
        public int Page { get; set; } = 1;
        public int Size
        {
            get => _size;
            set => _size = value > 100 ? 100 : value; // Limit size to 100
        }
    }
}
