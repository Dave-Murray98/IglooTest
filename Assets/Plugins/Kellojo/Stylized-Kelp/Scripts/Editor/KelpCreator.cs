using UnityEditor;
using UnityEngine;

namespace Kellojo.StylizedKelp.Editor
{
    public class KelpCreator {
        
        [MenuItem("GameObject/3D Object/Stylized Kelp", false, 10)]
        static void CreateKelp(MenuCommand menuCommand) {
            var kelpObj = new GameObject("Stylized Kelp");
            kelpObj.AddComponent<Kelp>();
        }
        
    }
}
