using UnityEngine;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System;
using System.Collections.Generic;
using System.IO.Ports;

public class GunJoystick : MonoBehaviour {
    public enum AxisOption
    {                                                    // Options for which axes to use                                                     
        Both,                                                                   // Use both
        OnlyHorizontal,                                                         // Only horizontal
        OnlyVertical                                                            // Only vertical
    }

    protected CrossPlatformInput.VirtualAxis horizontalVirtualAxis;               // Reference to the joystick in the cross platform input
    protected CrossPlatformInput.VirtualAxis verticalVirtualAxis;                 // Reference to the joystick in the cross platform input
    protected bool useX;                                                          // Toggle for using the x axis
    protected bool useY;                                                          // Toggle for using the Y axis
    protected CrossPlatformInput.VirtualAxis horizontalAimVirtualAxis;            // Reference to the joystick in the cross platform input
    protected CrossPlatformInput.VirtualAxis verticalAimVirtualAxis;              // Reference to the joystick in the cross platform input
    protected CrossPlatformInput.VirtualButton fireButton;

    public Vector2 deadZone = Vector2.zero;                                     // The dead zone where the joystick will not be regarded as having input
    public bool normalize;                                                      // Toggle for normalising the input from the joystick
    public AxisOption axesToUse = AxisOption.Both;                              // The options for the axes that the still will use
    public string horizontalAxisName = "Horizontal";                            // The name given to the horizontal axis for the cross platform input
    public string verticalAxisName = "Vertical";                                // The name given to the vertical axis for the cross platform input 
    public bool invertX = false;
    public bool invertY = false;

    const int defaultBufferLength = 48;
    public string _comPortName;
    public int _baudRate;

    private SerialPort _serialPort;

	// Use this for initialization
	void Awake () {
        CreateVirtualAxes();

        _serialPort = new SerialPort(_comPortName);
        //_serialPort.DtrEnable = true; // win32 hack to try to get DataReceived event to fire
        //_serialPort.RtsEnable = true; 
        _serialPort.BaudRate = _baudRate;
        _serialPort.DataBits = 8;
        _serialPort.Parity = Parity.None;
        _serialPort.StopBits = StopBits.One;
        _serialPort.ReadTimeout = 1; // since on windows we *cannot* have a separate read thread
        _serialPort.WriteTimeout = 1000;
        _serialPort.Open();
        //_serialPort.ReadExisting();
	}

    public void connectCallBack(IAsyncResult asyncConnect)
    {

    }

    private void CreateVirtualAxes()
    {
        // set axes to use
        useX = (axesToUse == AxisOption.Both || axesToUse == AxisOption.OnlyHorizontal);
        useY = (axesToUse == AxisOption.Both || axesToUse == AxisOption.OnlyVertical);

        // create new axes based on axes to use
        if (useX)
            horizontalVirtualAxis = new CrossPlatformInput.VirtualAxis(horizontalAxisName, false);
        if (useY)
            verticalVirtualAxis = new CrossPlatformInput.VirtualAxis(verticalAxisName, false);

        horizontalAimVirtualAxis = new CrossPlatformInput.VirtualAxis("HorizontalAim", false);
        verticalAimVirtualAxis = new CrossPlatformInput.VirtualAxis("VerticalAim", false);

        fireButton = new CrossPlatformInput.VirtualButton("Fire1", false);
    }
	
	// Update is called once per frame
	void Update () {

	}

    bool processingFlag = false;

    void FixedUpdate()
    {
        ReadJoystickData();
    }

    private void ReadJoystickData()
    {
        if(_serialPort.IsOpen)
        {
            // the read will time out since the board is only returning data every 100 ms, so swallow the exception
            try
            {
                var data = _serialPort.ReadLine();
                var dataSplit = data.Split(';');

                if (dataSplit[0] == "a")
                {
                    float ay = float.Parse(dataSplit[1]);
                    float ax = float.Parse(dataSplit[2]);
                    float az = float.Parse(dataSplit[3]);

                    ProcessAccelerometerData(ax, ay, az);
                }

                if (dataSplit[0] == "j") // this means we received a good joystick string
                {
                    float jx = float.Parse(dataSplit[1]);
                    float jy = float.Parse(dataSplit[2]);

                    if (!processingFlag)
                        ProcessJoystickData(jx, jy);
                }

                if(dataSplit[0] == "b")
                {
                    int buttonState = int.Parse(dataSplit[1]);
                    if(buttonState == 1)
                    {
                        fireButton.Pressed();
                    }
                    else
                    {
                        fireButton.Released();
                    }
                    //CrossPlatformInput.
                }

                _serialPort.DiscardInBuffer();
            }
            catch(TimeoutException)
            {

            }

        }
    }

    float movementThreshold = 500f;

    private void ProcessAccelerometerData(float x, float y, float z)
    {
        //float px = 0f;
        //float py = 0f;

        //if(Mathf.Abs(x) > movementThreshold)
        //{
        //    px = 1.0f * (x / 20000f);
        //}

        //Debug.Log(px);

        UpdateVirtualAimAxes(x, y);
    }

    private void ProcessJoystickData(float joystickX, float joystickY)
    {
        //Debug.Log(joystickX);
        //Debug.Log(joystickY);

        processingFlag = true;

        //float modifiedX = 0;
        //ConvertRawValue(joystickX, ref modifiedX);

        //float modifiedY = 0;
        //ConvertRawValue(joystickY, ref modifiedY);

        DeadZoneAndNormaliseAxes(ref joystickX, ref joystickY);
        AdjustAxesIfInverted(ref joystickX, ref joystickY);
        UpdateVirtualAxes(joystickX, joystickY);

        processingFlag = false;
    }

    private void ConvertRawValue(float rawValue, ref float modifiedValue)
    {
        if (rawValue < 512 && rawValue > 0)
        {
            modifiedValue = -1.0f - (-1.0f * (rawValue / 512f));
        }
        else if (rawValue > 512 && rawValue < 1024)
        {
            modifiedValue = 1.0f - (1.0f * ((1024 - rawValue) / 512f));
        }
        else if (rawValue == 0)
        {
            modifiedValue = -1;
        }
        else if (rawValue == 1024)
        {
            modifiedValue = 1;
        }
    }

    private void DeadZoneAndNormaliseAxes(ref float modifiedX, ref float modifiedY)
    {
        // Adjust for dead zone	
        var absoluteX = Mathf.Abs(modifiedX);
        var absoluteY = Mathf.Abs(modifiedY);


        if (absoluteX < deadZone.x)
        {
            // Report the joystick as being at the center if it is within the dead zone
            modifiedX = 0;
        }
        else if (normalize)
        {
            // Rescale the output after taking the dead zone into account
            modifiedX = Mathf.Sign(modifiedX) * (absoluteX - deadZone.x) / (1 - deadZone.x);
        }
        if (absoluteY < deadZone.y)
        {
            // Report the joystick as being at the center if it is within the dead zone
            modifiedY = 0;
        }
        else if (normalize)
        {
            // Rescale the output after taking the dead zone into account
            modifiedY = Mathf.Sign(modifiedY) * (absoluteY - deadZone.y) / (1 - deadZone.y);
        }
    }
    private void AdjustAxesIfInverted(ref float modifiedX, ref float modifiedY)
    {
        // Adjust for inversions
        modifiedX *= invertX ? -1 : 1;
        modifiedY *= invertY ? -1 : 1;
    }

    private void UpdateVirtualAxes(float modifiedX, float modifiedY)
    {
        //update the relevant axes
        if (useX)
            horizontalVirtualAxis.Update(modifiedX);
        if (useY)
            verticalVirtualAxis.Update(modifiedY);
    }

    private void UpdateVirtualAimAxes(float aimX, float aimY)
    {
        horizontalAimVirtualAxis.Update(aimX);
        verticalAimVirtualAxis.Update(aimY);
    }
}
