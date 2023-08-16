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
        public string Type { get; set; }
        public int Id { get; set; }
        public string Name { get; set; }
        public bool State { get; set; } 
              

        public EGPI(int id, string name) 
        {
            Type = "GPO"; //Hardcoded, to make AutoCam parsing easier. 
            Name = name;
            Id = id;    
            State = false;
        }

    }
    
}
