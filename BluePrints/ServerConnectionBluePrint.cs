using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "connection", menuName = "BluePrints/Connecton")]
[InlineEditor]
public class ServerConnectionBluePrint : ScriptableObject
{
    public bool IsActive = true;
    public bool IsRandomLocalPort = true;

    [Space(20)]
    public string Address = "localhost";
    public int Port = 8080;
    public int LocalPort = 4040;
    public string ServerKey = "ClausUmbrella";

    private void OnEnable()
    {
        if (IsRandomLocalPort)
            LocalPort = Random.Range(6000, 8500);
    }
}