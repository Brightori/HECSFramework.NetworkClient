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
    private int localPort = 4040;
    public int LocalPort => localPort == 0 ? localPort = Random.Range(6000, 8500) : localPort;
    public string ServerKey = "ClausUmbrella";

    private void OnEnable()
    {
        if (IsRandomLocalPort)
            localPort = Random.Range(6000, 8500);
    }
}