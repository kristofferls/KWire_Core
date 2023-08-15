using Lawo.EmberPlusSharp.Model;

namespace KWire_Core.Models.DHD 
{

    /// <summary>
    /// Represents a DHD Series-52 EmBER+ root
    /// </summary>

    public class DHD52Root : Root<DHD52Root> //,IDeviceProviderRoot<BaseNode>
    {
        [Element(Identifier = "Device")]
        //public FieldNode<BaseNode>? BaseNode { get; private set; }

        public BaseNode BaseNode { get; private set; }
    }

    /*
    public interface IDeviceProviderRoot<T> where T : FieldNode<T>
    {
        public FieldNode<T>? BaseNode { get; }
    }
    */

    public interface IDHDBase 
    {
        public FieldNode<IdentityNode>? IdentityNode { get;}

        public GPONode gPO { get;}

        public GPINode gPA { get;}

    }

    public sealed class BaseNode : FieldNode<BaseNode> //, IDeviceBaseNode<IdentityNode>
    {
        [Element(Identifier = "Identity")]
        public IdentityNode? IdentityNode { get; private set; }

        [Element(Identifier = "GPI")]
        public GPINode? GPINode { get; private set; }

        [Element(Identifier = "GPO")]
        public GPONode? GPONode { get; private set; }

        [Element(Identifier = "Channels")]
        public ChannelsNode? Channels { get; private set; }

        [Element(Identifier = "GlobalLabels")]
        public GlobalLabelsNode? GlobalLabels { get; private set; }
        
        [Element(Identifier = "Routing")]
        public RoutingNode? Routing { get; private set; }
    }

    public sealed class IdentityNode : FieldNode<IdentityNode>
    {
        // Company
        // Product
        // Firmwareversion
        // DHD-ember-plus-struct
    }

    public sealed class GPINode : DynamicFieldNode<GPINode> 
    {
    }

    public sealed class GPONode : DynamicFieldNode<GPONode> 
    {
        //[Element(Identifier = "Description")]
        //public Description? GPODescription {  get; private set; }
    }

    public sealed class Description : DynamicFieldNode<Description>
    {
    }
    public sealed class ChannelsNode : DynamicFieldNode<ChannelsNode> 
    {
    }

    public sealed class GlobalLabelsNode : DynamicFieldNode<GlobalLabelsNode> 
    {
    }

    public sealed class RoutingNode : DynamicFieldNode<RoutingNode> 
    {
    
    }

    

}
