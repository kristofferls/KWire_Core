using Lawo.EmberPlusSharp.Model;

namespace KWire
{
    public class VirtualGeneralPurposeIO
    {
        public string Name { get; set; }
        public IParameter TreeParameter { get; set; }
        public bool IsActive { get; set; }

    }

    public class EGPI 
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool State { get; set; } 
              

        public EGPI(int id, string name) 
        {
            Name = name;
            Id = id;    
            State = false;
        }

    }
    
}
