using System;
using CanvasCovers.Models.Products.LiftBlanket;

namespace CanvasCovers.UI.Products.LiftBlanket
{
    // Fired by LiftBlanketWindow when the operator clicks Generate and the
    // form has validated. The subscriber runs the generator; if it throws,
    // the subscriber should set Cancel = true so the dialog stays open and
    // the operator can fix inputs and try again instead of having the
    // window vanish with the error.
    public sealed class GenerateRequestedEventArgs : EventArgs
    {
        public GenerateRequestedEventArgs(LiftBlanketJob job)
        {
            Job = job;
        }

        public LiftBlanketJob Job { get; }

        public bool Cancel { get; set; }
    }
}
