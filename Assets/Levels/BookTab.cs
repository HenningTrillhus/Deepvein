using UnityEngine;
using UnityEngine.EventSystems;

namespace DeepVain.Levels
{
    /// <summary>
    /// En fane på høyre kant av boka. Klikk bytter side: sekken (inventory) eller levels.
    /// Lages av menyen DeepVain > Levels > 2 - Build level book (en på hver bok). Krever LevelBookUI i scenen.
    /// </summary>
    public class BookTab : MonoBehaviour, IPointerClickHandler
    {
        public enum Target { Inventory, Levels }
        public Target target = Target.Levels;

        public void OnPointerClick(PointerEventData e)
        {
            if (LevelBookUI.Instance != null) LevelBookUI.Instance.ShowPage(target == Target.Levels);
        }
    }
}
