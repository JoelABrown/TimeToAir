using BMDSwitcherAPI;

namespace Mooseware.TimeToAir;

public delegate void SwitcherEventHandler(object sender, object args);

////class SwitcherMonitor : IBMDSwitcherCallback
////{
////    // Events:
////    public event SwitcherEventHandler SwitcherDisconnected;

////    public SwitcherMonitor()
////    {
////    }

////    void IBMDSwitcherCallback.Notify(_BMDSwitcherEventType eventType, _BMDSwitcherVideoMode coreVideoMode)
////    {
////        if (eventType == _BMDSwitcherEventType.bmdSwitcherEventTypeDisconnected)
////        {
////            if (SwitcherDisconnected != null)
////                SwitcherDisconnected(this, null);
////        }
////    }
////}


// TODO: Follow this thread to see if we can hook the event that is fired when the Aux Input is changed.
////var gar = _BMDSwitcherInputAuxEventType.bmdSwitcherInputAuxEventTypeInputSourceChanged;

public class MixEffectBlockMonitor : IBMDSwitcherMixEffectBlockCallback
{
    // TO USE THIS CLASS (FOR CATCHING EVENTS RAISED BY THE ATEM)...
    // ADD THE FOLLOWING CODE TO A UI WINDOW'S CTOR...

    // Wire up ATEM switch event of interest.
    // Note: this invoke pattern ensures our callback is called in the main thread. We are making double
    // use of lambda expressions here to achieve this.
    // Essentially, the events will arrive at the callback class (implemented by our monitor classes)
    // on a separate thread. We must marshal these to the main thread, and we're doing this by calling
    // invoke on the Windows Forms object. The lambda expression is just a simplification.
    //
    // _mixEffectBlockMonitor = new Tachnit.AtemApi.MixEffectBlockMonitor();
    // _mixEffectBlockMonitor.ProgramInputChanged += new Tachnit.AtemApi.SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => AtemProgramInputChanged())));

    // THEN ADD A HANDLER TO THE UI CLASS SUCH AS...
    //
    // private void AtemProgramInputChanged()   // This sub name must be the same as specified above ^^^
    // {
    //     SetOnAirDisplay();
    // }

    // Events:
    public event SwitcherEventHandler ProgramInputChanged;
    public event SwitcherEventHandler PreviewInputChanged;
    ////public event SwitcherEventHandler TransitionFramesRemainingChanged;
    ////public event SwitcherEventHandler TransitionPositionChanged;
    ////public event SwitcherEventHandler InTransitionChanged;

    public MixEffectBlockMonitor()
    {
    }

    void IBMDSwitcherMixEffectBlockCallback.Notify(_BMDSwitcherMixEffectBlockEventType eventType)
    {
        switch (eventType)
        {
            case _BMDSwitcherMixEffectBlockEventType.bmdSwitcherMixEffectBlockEventTypeProgramInputChanged:
                ProgramInputChanged?.Invoke(this, null);
                break;
            case _BMDSwitcherMixEffectBlockEventType.bmdSwitcherMixEffectBlockEventTypePreviewInputChanged:
                PreviewInputChanged?.Invoke(this, null);
                break;
                ////case _BMDSwitcherMixEffectBlockEventType.bmdSwitcherMixEffectBlockEventTypeTransitionFramesRemainingChanged:
                ////    if (TransitionFramesRemainingChanged != null)
                ////        TransitionFramesRemainingChanged(this, null);
                ////    break;
                ////case _BMDSwitcherMixEffectBlockEventType.bmdSwitcherMixEffectBlockEventTypeTransitionPositionChanged:
                ////    if (TransitionPositionChanged != null)
                ////        TransitionPositionChanged(this, null);
                ////    break;
                ////case _BMDSwitcherMixEffectBlockEventType.bmdSwitcherMixEffectBlockEventTypeInTransitionChanged:
                ////    if (InTransitionChanged != null)
                ////        InTransitionChanged(this, null);
                ////    break;
        }
    }

}

////class InputMonitor : IBMDSwitcherInputCallback
////{
////    // Events:
////    public event SwitcherEventHandler LongNameChanged;

////    private IBMDSwitcherInput m_input;
////    public IBMDSwitcherInput Input { get { return m_input; } }

////    public InputMonitor(IBMDSwitcherInput input)
////    {
////        m_input = input;
////    }

////    void IBMDSwitcherInputCallback.Notify(_BMDSwitcherInputEventType eventType)
////    {
////        switch (eventType)
////        {
////            case _BMDSwitcherInputEventType.bmdSwitcherInputEventTypeLongNameChanged:
////                if (LongNameChanged != null)
////                    LongNameChanged(this, null);
////                break;
////        }
////    }
////}
