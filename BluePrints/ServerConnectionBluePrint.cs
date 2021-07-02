using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "connection", menuName = "BluePrints/Connecton")]
[InlineEditor]
public class ServerConnectionBluePrint : ScriptableObject
{
    public bool IsActive = true;

    [Space(20)]
    public string Address = "localhost";
    public int Port = 8080;
    public int LocalPort = 4040;
    public string ServerKey = "ClausUmbrella";
}