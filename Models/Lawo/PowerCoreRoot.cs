using KWire;
using Lawo.EmberPlusSharp.Model;

namespace KWire_Core.Models.Lawo
{
    /// <summary>
    /// Represents a Lawo PowerCore EmBER+ root
    /// Edited to fit NRK AutoCam EGPIO by removing nodes that are not present in the tree. 
    /// Is there a way to make this more dynamic, and not reliant upon a hardcoded structure? 
    /// </summary>
    public class PowerCoreRubyRoot : Root<PowerCoreRubyRoot> //, IDeviceProviderRoot<BaseNode>
    {
        [Element(Identifier = "Ruby")]
        public BaseNode BaseNode { get; private set; }
    }

    public interface IPowerCoreRubyBase
    {
        public FieldNode<IdentityNode>? IdentityNode { get; }
        public SourcesNode? SourcesNode { get; }
        public SumsNode? SumsNode { get; }
        //public StreamingNode? RavennaNode { get; }
        public GpioNode? Gpio { get; }
    }

    public sealed class BaseNode : FieldNode<BaseNode> //, IPowerCoreRubyBase //, IDeviceBaseNode<IdentityNode>
    {
        [Element(Identifier = "identity")]
        public IdentityNode? IdentityNode { get; private set; }

        [Element(Identifier = "Sources")]
        public SourcesNode? SourcesNode { get; private set; }

        [Element(Identifier = "Sums")]
        public SumsNode? SumsNode { get; private set; }

        //[Element(Identifier = "RAVENNA")]
        //public StreamingNode? RavennaNode { get; private set; }

        [Element(Identifier = "GPIOs")]
        public GpioNode? Gpio { get; private set; }
    }

    public sealed class IdentityNode : FieldNode<IdentityNode>
    {
    }

    public sealed class SourcesNode : DynamicFieldNode<SourcesNode>
    {
    }

    public sealed class SumsNode : DynamicFieldNode<SumsNode>
    {
    }

    //public sealed class StreamingNode : DynamicFieldNode<StreamingNode>
    //{
    //}

    public sealed class GpioNode : FieldNode<GpioNode>
    {
        [Element(Identifier = "EGPIO_AUTOCAM")]
        public Monitor? EGPIO_AUTOCAM { get; private set; }
    }

    public sealed class Monitor : FieldNode<Monitor>
    {
        [Element(Identifier = "Output Signals")]
        public OutputSignalNode? OutputSignals { get; private set; }
        [Element(Identifier = "Input Signals")]
        public InputSignalNode? InputSignals { get; private set; }
    }

    public sealed class OutputSignalNode : DynamicFieldNode<OutputSignalNode>
    {
    }

    public sealed class InputSignalNode : DynamicFieldNode<InputSignalNode>
    {
    }
}
