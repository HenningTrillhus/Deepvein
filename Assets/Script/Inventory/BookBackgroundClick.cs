using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DeepVain.Items
{
    /// <summary>On the book picture: a click on empty paper closes the popup.</summary>
    public class BookBackgroundClick : MonoBehaviour, IPointerClickHandler
    {
        public Action clicked;
        public void OnPointerClick(PointerEventData e) { if (clicked != null) clicked(); }
    }
}
