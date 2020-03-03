using System;
using System.Text;
// Crrestron
using Crestron.SimplSharp;
//MQTT
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

public class AWSIoT
{
    private MqttClient client;

    string mqttServer = "broker.hivemq.com";
    //int mqttPort = 1883;
    string mqttTopic = "/264b17c7-3fde-46bc-b46f-8af76ee6d445";
    private ushort _msgId = 0;

    // Constructor
    public AWSIoT()
	{
        CrestronConsole.PrintLine("[MQTT] Contructor");
	}

    private void Client_ConnectionClosed(object sender, EventArgs e)
    {
        CrestronConsole.PrintLine("[MQTT] Connection Closed");
    }

    private void Client_MqttMsgUnsubscribed(object sender, MqttMsgUnsubscribedEventArgs e)
    {
        CrestronConsole.PrintLine("[MQTT] Message {0} Unsubscribed", e.MessageId);
    }

    private void Client_MqttMsgSubscribed(object sender, MqttMsgSubscribedEventArgs e)
    {
        CrestronConsole.PrintLine("[MQTT] Message {0} Unsubscribed", e.MessageId);
    }

    private static void Client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
    {
        CrestronConsole.PrintLine("[MQTT] Message {0} received {1}", e.Topic, Encoding.UTF8.GetString(e.Message));            
    }

    private static void Client_MqttMsgPublished(object sender, MqttMsgPublishedEventArgs e)
    {
        CrestronConsole.PrintLine("[MQTT] Message {0} published {1}", e.MessageId, e.IsPublished);
    }


    public void Start()
    {
        CrestronConsole.PrintLine("[MQTT] Start");

        // Create a client instance
        client = new MqttClient(mqttServer);

        client.ConnectionClosed += Client_ConnectionClosed;
        client.MqttMsgSubscribed += Client_MqttMsgSubscribed;
        client.MqttMsgUnsubscribed += Client_MqttMsgUnsubscribed;
        client.MqttMsgPublishReceived += Client_MqttMsgPublishReceived;
        client.MqttMsgPublished += Client_MqttMsgPublished;


        // Generate a unique id for our client
        string clientId = Guid.NewGuid().ToString();

        // Connect
        byte code = client.Connect(clientId);
        CrestronConsole.PrintLine("[MQTT] Connect code : {0}", code);

        if (client.IsConnected)
            CrestronConsole.PrintLine("[+] MQTT Connected");

        // Subscribe to the topic
        client.Subscribe(new String[] { mqttTopic }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });

        _msgId = client.Publish(mqttTopic, Encoding.ASCII.GetBytes("Test"));

    }

    public void register()
    {

    }

    public void sendMessage(string message)
    {
        client.Publish(mqttTopic, Encoding.UTF8.GetBytes(message));
    }
}
