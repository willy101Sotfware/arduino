using System;
using System.IO.Ports;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using FTD2XX_NET;
using System.Runtime.InteropServices;

public class MetavixDispenser : IDisposable
{
    // Constantes de comunicación
    private const int BAUDRATE_PC = 57600;
    private const int BAUDRATE_DISPENSER = 115200;
    private const int BAUDRATE_ACCEPTANCE = 9600;

    // Constantes de billetes
    private const int NUMBER_OF_CASSETTES = 3;
    private const int NOTE_UP = 50000;
    private const int NOTE_MIDDLE = 10000;
    private const int NOTE_BOTTOM = 2000;

    // Constantes de monedas
    private const int NUMBER_OF_HOPPERS = 3;
    private const int HOPPER_ONE = 500;
    private const int HOPPER_TWO = 200;
    private const int HOPPER_THREE = 100;

    // Pines para relés y sensores
    private const string RELAY_ONE_PIN_NAME = "A4";
    private const string RELAY_TWO_PIN_NAME = "A3";
    private const string RELAY_THREE_PIN_NAME = "A2";
    private const string SENSOR_ONE_PIN_NAME = "4";
    private const string SENSOR_TWO_PIN_NAME = "3";
    private const string SENSOR_THREE_PIN_NAME = "2";
    private const string COUNTER_PIN_NAME = "7";
    private const string ACTIVATOR_PIN_NAME = "6";

    // Constantes de tiempo y tamaños
    private const int MOTOR_TIMEOUT = 3000;
    private const int RESPONSE_SIZE = 254;
    private const int MESSAGE_SIZE = 254;
    private const int COMMANDS_SIZE = 50;
    private const int TIMEOUT = 5000;

    // Estados del sistema
    private const int STATE_ACCEPTANCE_STATUS = 0;
    private const int STATE_ACCEPTANCE_STACK = 1;
    private const int STATE_ACCEPTANCE_SEND_ACK = 2;
    private const int STATE_ACCEPTANCE_GET_DATA = 3;
    private const int STATE_ACCEPTANCE_INITIALIZE = 4;
    private const int STATE_ACCEPTANCE_RESET = 5;
    private const int STATE_ACCEPTANCE_FATAL_ERROR = 6;
    private const int STATE_ACCEPTANCE_VEND_VALID = 7;
    private const int STATE_WAITTING_FOR_COMMAND = 8;
    private const int STATE_ENCRYPT = 9;
    private const int STATE_PARSE_PC_COMMAND = 10;
    private const int STATE_ACCEPTANCE = 11;
    private const int STATE_DISPENSE = 12;
    private const int STATE_DISPENSER_COUNT_NOTES = 13;
    private const int STATE_DISPENSER_SEND_COMMAND = 14;
    private const int STATE_DISPENSER_GETTING_COMMAND = 15;
    private const int STATE_DISPENSER_ANSWER_PC = 16;
    private const int STATE_DISPENSER_INITIALIZE = 17;
    private const int STATE_DISPENSER_PARSE_INFORMATION = 18;
    private const int STATE_COUNT_MONEY_FOR_RETURN = 20;
    private const int STATE_READING_COIN = 21;
    private const int STATE_SENDING_MESSAGE_TO_PC = 22;
    private const int STATE_TURN_ON_MOTORS = 23;

    // Comandos ASCII
    private const byte ASCII_ENQ = 0x05;
    private const byte ASCII_ACK = 0x06;
    private const byte NAK = 0x15;
    private const byte EOT = 0x04;
    private const byte CAN = 0x18;
    private const byte STX = 0x02;
    private const byte ETX = 0x03;
    private const byte US = 0x1F;

    // Comandos del dispensador
    private const byte DISPENSER_INITIALIZE = 0x54;
    private const byte DISPENSER_GET_SENSOR = 0x53;
    private const byte DISPENSER_GET_VERSION = 0x56;
    private const byte DISPENSER_GET_PARAMETER = 0x47;
    private const byte DISPENSER_SET_PARAMETER = 0x50;
    private const byte DISPENSER_DISPENSE = 0x44;
    private const byte DISPENSER_CALIBRATION_CIS = 0x43;
    private const byte DISPENSER_SET_PROGRAM = 0x4D;
    private const byte DISPENSER_GET_QUERY = 0x51;
    private const byte DISPENSER_FIRMWARE_UPGRADE = 0x55;
    private const byte DISPENSER_GET_EEP = 0x46;
    private const byte DISPENSER_SET_EEP = 0x45;
    private const byte DISPENSER_EJECT = 0x4A;
    private const byte DISPENSER_REGISTER = 0x52;
    private const byte DISPENSER_DEBUG_DISPLAY_MODE = 0x42;

    // Estados de respuesta
    private const byte ENABLE = 0x11;
    private const byte ACCEPTING = 0x12;
    private const byte ESCROW = 0x13;
    private const byte STACKING = 0x14;
    private const byte VEND_VALID = 0x15;
    private const byte STACKED = 0x16;
    private const byte REJECTING = 0x17;
    private const byte RETURNING = 0x18;
    private const byte HOLDING = 0x19;
    private const byte DISABLE = 0x1A;
    private const byte INITIALIZE = 0x1B;

    // Estados de encendido
    private const byte POWER_UP = 0x40;
    private const byte POWER_UP_WITH_BILL_IN_ACCEPTOR = 0x41;
    private const byte ENABLE_UP_WITH_BILL_IN_STACKER = 0x42;

    // Estados de error
    private const byte STACKER_FULL = 0x43;
    private const byte STACKER_OPEN = 0x44;
    private const byte JAM_IN_ACCEPTOR = 0x45;
    private const byte JAM_IN_STACKER = 0x46;
    private const byte PAUSE = 0x47;
    private const byte CHEATED = 0x48;
    private const byte FAILURE = 0x49;
    private const byte COMMUNICATION_ERROR = 0x4A;

    // Comandos básicos
    private const byte COMMAND_HEADER = 0x02;
    private const byte SET_STATUS = 0x03;
    private const byte CLEAR_STATUS = 0x04;
    private const byte RESET = 0x0A;

    // Constantes adicionales
    private const byte SET_HOLD = 0x44;
    private const byte SET_WAIT = 0x45;
    private const byte ENQ = 0x05;
    private const byte INVALID_COMMAND = 0x48;
    private const byte ENABLE_DENOMINATION = 0xC0;
    private const byte SECURITY = 0xC1;
    private const byte COMMUNICATION_MODE = 0xC2;
    private const byte INHIBIT = 0xC3;
    private const byte DIRECTION = 0xC4;
    private const byte OPTIONAL_FUNCTION = 0xC5;
    private const byte SET_STACK_1 = 0x41;

    // Variables del sistema
    private SerialPort serialPort;
    private int currentState;
    private bool debugFlag = false;
    private int[] coinsToReturn = new int[NUMBER_OF_HOPPERS];
    private int[] coinsReturned = new int[NUMBER_OF_HOPPERS];
    private bool acceptFlag = false;
    private bool coinsFlag = false;
    private int quantity;
    private int quantityCoins;
    private Dictionary<string, (char, char)> errorsDispenser = new Dictionary<string, (char, char)>();
    private Dictionary<string, string> errorsDispenserResponse = new Dictionary<string, string>();
    private bool[] flagCassetteDispense = new bool[NUMBER_OF_CASSETTES];
    private byte[] response = new byte[RESPONSE_SIZE];
    private byte[] message = new byte[MESSAGE_SIZE];
    private byte[] commands = new byte[COMMANDS_SIZE];
    private int responseCounter = 0;
    private int messageCounter = 0;
    private int commandCounter = 0;
    private Dictionary<byte, int> statusResponseTable = new Dictionary<byte, int>();
    private Dictionary<byte, int> flagForResponseAction = new Dictionary<byte, int>();
    private Dictionary<int, string> pinSensorNames = new Dictionary<int, string>();
    private Dictionary<int, string> pinRelayNames = new Dictionary<int, string>();
    private Dictionary<int, int> staticCoinValues = new Dictionary<int, int>();
    private Dictionary<int, bool> sensorStates = new Dictionary<int, bool>();
    private Dictionary<int, bool> relayStates = new Dictionary<int, bool>();
    private Dictionary<string, string> errorMessages = new Dictionary<string, string>();
    private int[] lastSensorState = new int[NUMBER_OF_HOPPERS];

    // Variables adicionales
    private int lastState = STATE_WAITTING_FOR_COMMAND;
    private bool flagForPowerUp = true;
    private uint[] hopperQuantity = new uint[NUMBER_OF_HOPPERS];
    private uint[] noteCassette = new uint[4];
    private uint[] quantityBox = new uint[4];
    private byte tryCatchDispense = 0;
    private int[] valuesOfCassettes = new int[6];
    private int szDispCmd = 0;

    // Variables para el control de hardware
    private FTDI ftdiDevice;
    private bool isHardwareInitialized = false;
    private byte[] pinMask = new byte[1];
    private byte[] pinStates = new byte[1];

    public MetavixDispenser()
    {
        ftdiDevice = new FTDI();
        serialPort = new SerialPort();
        commands = new byte[256];
        response = new byte[0];
        isHardwareInitialized = false;
        debugFlag = true;
    }

    public MetavixDispenser(string portName = "COM1")
    {
        try
        {
            // Inicializar comunicación serial
            serialPort = new SerialPort(portName, BAUDRATE_ACCEPTANCE, Parity.None, 8, StopBits.One);
            serialPort.DataReceived += SerialPort_DataReceived;
            serialPort.ReadTimeout = TIMEOUT;
            serialPort.WriteTimeout = TIMEOUT;
            serialPort.Open();
            
            // Inicializar hardware FTDI
            InitializeHardware();
            
            InitializeSystem();
            FillErrorArray();
            ClearCoinsToReturnAndReturned();
            StatusCheckerFilling();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing dispenser: {ex.Message}");
            throw;
        }
    }

    private void InitializeHardware()
    {
        try
        {
            uint deviceCount = 0;
            ftdiDevice.GetNumberOfDevices(ref deviceCount);

            if (deviceCount == 0)
            {
                throw new Exception("No FTDI devices found");
            }

            FTDI.FT_STATUS status = ftdiDevice.OpenByIndex(0);
            if (status != FTDI.FT_STATUS.FT_OK)
            {
                throw new Exception($"Failed to open FTDI device: {status}");
            }

            // Configurar el dispositivo FTDI
            ftdiDevice.SetBitMode(0xFF, FTDI.FT_BIT_MODES.FT_BIT_MODE_SYNC_BITBANG);
            ftdiDevice.SetBaudRate(9600);

            isHardwareInitialized = true;
            if (debugFlag) Console.WriteLine("Hardware initialized successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing hardware: {ex.Message}");
            throw;
        }
    }

    private async Task Initialize()
    {
        try
        {
            InitializePinSensorNames();
            InitializeRelayNames();
            InitializeStaticCoinValues();
            await Task.Delay(100);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing: {ex.Message}");
        }
    }

    private void SetPinState(string pinName, bool state)
    {
        if (!isHardwareInitialized) return;

        try
        {
            // Convertir nombre de pin a número de bit
            int bitPosition = GetBitPositionFromPin(pinName);
            if (bitPosition < 0) return;

            // Actualizar el estado del pin
            if (state)
                pinStates[0] |= (byte)(1 << bitPosition);
            else
                pinStates[0] &= (byte)~(1 << bitPosition);

            // Escribir al hardware
            uint bytesWritten = 0;
            ftdiDevice.Write(pinStates, 1, ref bytesWritten);

            if (debugFlag)
                Console.WriteLine($"Pin {pinName} set to {state}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting pin state: {ex.Message}");
        }
    }

    private bool ReadPinState(string pinName)
    {
        if (!isHardwareInitialized) return false;

        try
        {
            // Leer el estado actual
            uint bytesRead = 0;
            byte[] readBuffer = new byte[1];
            ftdiDevice.Read(readBuffer, 1, ref bytesRead);

            // Convertir nombre de pin a número de bit
            int bitPosition = GetBitPositionFromPin(pinName);
            if (bitPosition < 0) return false;

            // Retornar el estado del pin
            return (readBuffer[0] & (1 << bitPosition)) != 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading pin state: {ex.Message}");
            return false;
        }
    }

    private int GetBitPositionFromPin(string pinName)
    {
        // Convertir nombres de pines a posiciones de bits
        switch (pinName)
        {
            case "A4": return 0;
            case "A3": return 1;
            case "A2": return 2;
            case "4": return 3;
            case "3": return 4;
            case "2": return 5;
            case "7": return 6;
            case "6": return 7;
            default: return -1;
        }
    }

    private void StatusCheckerFilling()
    {
        statusResponseTable[ENABLE] = STATE_ACCEPTANCE_STATUS;
        statusResponseTable[ACCEPTING] = STATE_ACCEPTANCE_STATUS;
        statusResponseTable[ESCROW] = STATE_ACCEPTANCE_STACK;
        statusResponseTable[STACKING] = STATE_ACCEPTANCE_STATUS;
        statusResponseTable[VEND_VALID] = STATE_ACCEPTANCE_VEND_VALID;
        statusResponseTable[STACKED] = STATE_ACCEPTANCE_STATUS;
        statusResponseTable[REJECTING] = STATE_ACCEPTANCE_GET_DATA;
        statusResponseTable[RETURNING] = STATE_ACCEPTANCE_STATUS;
        statusResponseTable[HOLDING] = STATE_ACCEPTANCE_FATAL_ERROR;
        statusResponseTable[DISABLE] = STATE_WAITTING_FOR_COMMAND;
        statusResponseTable[INITIALIZE] = STATE_ACCEPTANCE_INITIALIZE;
        statusResponseTable[POWER_UP] = STATE_ACCEPTANCE_RESET;
        statusResponseTable[POWER_UP_WITH_BILL_IN_ACCEPTOR] = STATE_ACCEPTANCE_RESET;
        statusResponseTable[ENABLE_UP_WITH_BILL_IN_STACKER] = STATE_ACCEPTANCE_RESET;
        statusResponseTable[STACKER_FULL] = STATE_ACCEPTANCE_STATUS;
        statusResponseTable[STACKER_OPEN] = STATE_ACCEPTANCE_STATUS;
        statusResponseTable[JAM_IN_ACCEPTOR] = STATE_ACCEPTANCE_STATUS;
        statusResponseTable[JAM_IN_STACKER] = STATE_ACCEPTANCE_STATUS;
        statusResponseTable[PAUSE] = STATE_ACCEPTANCE_STATUS;
        statusResponseTable[CHEATED] = STATE_ACCEPTANCE_STATUS;
        statusResponseTable[FAILURE] = STATE_ACCEPTANCE_FATAL_ERROR;
        statusResponseTable[COMMUNICATION_ERROR] = STATE_ACCEPTANCE_STATUS;
        statusResponseTable[ENQ] = STATE_ACCEPTANCE_STATUS;
        statusResponseTable[ASCII_ACK] = STATE_ACCEPTANCE_STATUS;
        statusResponseTable[INVALID_COMMAND] = STATE_ACCEPTANCE_FATAL_ERROR;
        statusResponseTable[ENABLE_DENOMINATION] = STATE_ACCEPTANCE_STATUS;
        statusResponseTable[SECURITY] = STATE_ACCEPTANCE_STATUS;
        statusResponseTable[COMMUNICATION_MODE] = STATE_ACCEPTANCE_STATUS;
        statusResponseTable[INHIBIT] = STATE_ACCEPTANCE_STATUS;
        statusResponseTable[DIRECTION] = STATE_ACCEPTANCE_STATUS;
        statusResponseTable[OPTIONAL_FUNCTION] = STATE_ACCEPTANCE_STATUS;
    }

    private void InitializeSystem()
    {
        currentState = STATE_ACCEPTANCE_INITIALIZE;
        ClearCoinsToReturnAndReturned();
        InitializePinSensorNames();
        InitializeRelayNames();
        InitializeStaticCoinValues();
        ClearBuffers();
        SetPredefinedConfiguration();
    }

    private void ClearBuffers()
    {
        Array.Clear(response, 0, response.Length);
        Array.Clear(message, 0, message.Length);
        Array.Clear(commands, 0, commands.Length);
        responseCounter = 0;
        messageCounter = 0;
        commandCounter = 0;
    }

    private async void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (serialPort.BytesToRead > 0)
        {
            int bytesToRead = serialPort.BytesToRead;
            byte[] buffer = new byte[bytesToRead];
            serialPort.Read(buffer, 0, bytesToRead);
            await ProcessReceivedData(buffer);
        }
    }

    private async Task ProcessReceivedData(byte[] data)
    {
        foreach (byte b in data)
        {
            switch (currentState)
            {
                case STATE_ACCEPTANCE_STATUS:
                    HandleAcceptanceStatus(b);
                    break;
                case STATE_ACCEPTANCE_STACK:
                    HandleAcceptanceStack(b);
                    break;
                case STATE_ACCEPTANCE_SEND_ACK:
                    HandleAcceptanceSendAck(b);
                    break;
                case STATE_DISPENSER_COUNT_NOTES:
                    HandleDispenserCountNotes(b);
                    break;
                case STATE_DISPENSER_SEND_COMMAND:
                    HandleDispenserSendCommand(b);
                    break;
                case STATE_COUNT_MONEY_FOR_RETURN:
                    HandleStateCountMoneyForReturn();
                    break;
                case STATE_TURN_ON_MOTORS:
                    HandleStateTurnOnMotors();
                    break;
                case STATE_READING_COIN:
                    HandleStateReadingCoin();
                    break;
                default:
                    if (debugFlag)
                        Console.WriteLine($"Unhandled state: {currentState}");
                    break;
            }
        }
    }

    private void HandleAcceptanceStatus(byte data)
    {
        response[responseCounter++] = data;
        if (responseCounter >= 3)
        {
            if (IsEnableCommand(response))
            {
                acceptFlag = true;
                SendResponse("RC:ON:AP");
                currentState = STATE_WAITTING_FOR_COMMAND;
            }
            ClearBuffers();
        }
    }

    private void HandleAcceptanceStack(byte data)
    {
        response[responseCounter++] = data;
        if (responseCounter >= 3)
        {
            if (IsStackedCommand(response))
            {
                SendResponse("RC:OK:AP");
                currentState = STATE_WAITTING_FOR_COMMAND;
            }
            ClearBuffers();
        }
    }

    private void HandleAcceptanceSendAck(byte data)
    {
        SendCommand(new byte[] { 0x02, 0x03, 0x06, 0x00, 0x00, 0x00, 0x00, 0x00 });
        currentState = STATE_WAITTING_FOR_COMMAND;
    }

    private async void HandleDispenserCountNotes(byte data)
    {
        await HandleStateDispenserCountNotes();
    }

    private async void HandleDispenserSendCommand(byte data)
    {
        await HandleStateDispenserParseResponse();
    }

    private async Task HandleStateDispenserCountNotes()
    {
        // Implementar la lógica de conteo de billetes
        currentState = STATE_COUNT_MONEY_FOR_RETURN;
    }

    private async Task HandleStateDispenserParseResponse()
    {
        // Implementar el análisis de respuesta del dispensador
        if (debugFlag)
        {
            DebugMetavix("State parse dispenser response");
        }
    }

    private async Task HandleStateCountMoneyForReturn()
    {
        if (coinsFlag)
        {
            // Implementar lógica de conteo de monedas
            currentState = STATE_TURN_ON_MOTORS;
        }
    }

    private async Task HandleStateTurnOnMotors()
    {
        // Implementar lógica de activación de motores
        if (debugFlag)
        {
            DebugMetavix("Turning on motors");
        }
        currentState = STATE_READING_COIN;
    }

    private async Task HandleStateReadingCoin()
    {
        // Implementar lógica de lectura de monedas
        if (debugFlag)
        {
            DebugMetavix("Reading coins");
        }
        // Implementar la lógica de lectura
    }

    private bool IsEnableCommand(byte[] cmd)
    {
        return cmd.Length >= 3 && cmd[0] == 0x02 && cmd[1] == 0x03 && cmd[2] == 0x11;
    }

    private bool IsStackedCommand(byte[] cmd)
    {
        return cmd.Length >= 3 && cmd[0] == 0x02 && cmd[1] == 0x03 && cmd[2] == 0x13;
    }

    private async Task SendCommand(byte[] command)
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            try
            {
                serialPort.Write(command, 0, command.Length);
                await Task.Delay(100); // Pequeña pausa para asegurar que el comando se envió
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending command: {ex.Message}");
            }
        }
    }

    private void SendResponse(string response)
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.WriteLine(response);
        }
    }

    private void FillErrorArray()
    {
        errorMessages.Clear();
        errorMessages.Add("STACKER_FULL", "Stacker is full");
        errorMessages.Add("STACKER_OPEN", "Stacker is open");
        errorMessages.Add("JAM_IN_ACCEPTOR", "Bill jam in acceptor");
        errorMessages.Add("JAM_IN_STACKER", "Bill jam in stacker");
        errorMessages.Add("PAUSE", "Device is paused");
        errorMessages.Add("CHEATED", "Cheated detected");
        errorMessages.Add("FAILURE", "Device failure");
        errorMessages.Add("COMMUNICATION_ERROR", "Communication error");
        errorMessages.Add("INSERTION_ERROR", "Bill insertion error");
        errorMessages.Add("STACK_MOTOR_FAILURE", "Stack motor failure");
        errorMessages.Add("TRANSPORT_MOTOR_FAILURE", "Transport motor failure");
        errorMessages.Add("SOLENOID_FAILURE", "Solenoid failure");
        errorMessages.Add("CASHBOX_NOT_READY", "Cashbox not ready");
        errorMessages.Add("VALIDATOR_HEAD_REMOVE", "Validator head removed");
        errorMessages.Add("BOOT_ROM_FAILURE", "Boot ROM failure");
        errorMessages.Add("RAM_FAILURE", "RAM failure");
    }

    private void ClearCoinsToReturnAndReturned()
    {
        Array.Clear(coinsToReturn, 0, coinsToReturn.Length);
        Array.Clear(coinsReturned, 0, coinsReturned.Length);
    }

    private void InitializePinSensorNames()
    {
        if (debugFlag) Console.WriteLine("Initializing pin sensor names...");
        
        // Inicializar los pines de sensores según el Arduino original
        pinSensorNames[0] = SENSOR_ONE_PIN_NAME;   // Pin 4 - 100
        pinSensorNames[1] = SENSOR_TWO_PIN_NAME;   // Pin 3 - 200
        pinSensorNames[2] = SENSOR_THREE_PIN_NAME; // Pin 2 - 500

        // Inicializar estados de los sensores y último estado
        for (int i = 0; i < NUMBER_OF_HOPPERS; i++)
        {
            sensorStates[i] = false;
            lastSensorState[i] = 0;
        }

        if (debugFlag) Console.WriteLine("Pin sensor names initialized");
    }

    private void InitializeRelayNames()
    {
        if (debugFlag) Console.WriteLine("Initializing pin relay names...");
        
        // Inicializar los pines de relés según el Arduino original
        pinRelayNames[0] = RELAY_ONE_PIN_NAME;   // A4 - 100
        pinRelayNames[1] = RELAY_TWO_PIN_NAME;   // A3 - 200
        pinRelayNames[2] = RELAY_THREE_PIN_NAME; // A2 - 500

        // Inicializar estados de los relés y hoppers
        for (int i = 0; i < NUMBER_OF_HOPPERS; i++)
        {
            relayStates[i] = false;
            hopperQuantity[i] = 0;
        }

        if (debugFlag) Console.WriteLine("Pin relay names initialized");
    }

    private void InitializeStaticCoinValues()
    {
        if (debugFlag) Console.WriteLine("Initializing static coin values...");
        
        // Inicializar los valores de monedas según el Arduino original
        staticCoinValues[0] = HOPPER_THREE; // 100
        staticCoinValues[1] = HOPPER_TWO;   // 200
        staticCoinValues[2] = HOPPER_ONE;   // 500

        // Inicializar valores de cassettes
        for (int i = 0; i < 4; i++)
        {
            noteCassette[i] = 0;
            quantityBox[i] = 0;
        }

        if (debugFlag)
        {
            foreach (var kvp in staticCoinValues)
            {
                Console.WriteLine($"Hopper {kvp.Key}: {kvp.Value}");
            }
        }
    }

    private void SetRelayState(int relayIndex, bool state)
    {
        if (relayStates.ContainsKey(relayIndex))
        {
            relayStates[relayIndex] = state;
            SetPinState(pinRelayNames[relayIndex], state);
        }
    }

    private bool GetSensorState(int sensorIndex)
    {
        if (sensorStates.ContainsKey(sensorIndex))
        {
            // Leer el estado real del sensor
            bool currentState = ReadPinState(pinSensorNames[sensorIndex]);
            sensorStates[sensorIndex] = currentState;
            return currentState;
        }
        return false;
    }

    private void UpdateSensorStates()
    {
        foreach (var sensor in pinSensorNames.Keys)
        {
            // Leer el estado real del sensor
            bool currentState = GetSensorState(sensor);
            if (currentState != sensorStates[sensor])
            {
                sensorStates[sensor] = currentState;
                if (debugFlag)
                {
                    Console.WriteLine($"Sensor {sensor} ({pinSensorNames[sensor]}) changed to {currentState}");
                }
            }
        }
    }

    private int GetCoinValueFromSensor(int sensorIndex)
    {
        if (staticCoinValues.ContainsKey(sensorIndex))
        {
            return staticCoinValues[sensorIndex];
        }
        return 0;
    }

    private void FillErrorsDispenser()
    {
        errorsDispenser.Add("NORMAL", ('0', '0'));
        errorsDispenser.Add("UNDEFINED", ('0', '1'));
        errorsDispenser.Add("CONTINUOUS_5_NOTES", ('0', '2'));
        // Agregar más errores según sea necesario
        
        errorsDispenserResponse.Add("NORMAL", "Normal operation");
        errorsDispenserResponse.Add("UNDEFINED", "Undefined error");
        errorsDispenserResponse.Add("CONTINUOUS_5_NOTES", "Continuous 5 notes reject error");
        // Agregar más mensajes según sea necesario

        for (int i = 0; i < flagCassetteDispense.Length; i++)
        {
            flagCassetteDispense[i] = false;
        }
    }

    private void DecimalToHexa(ushort value, int len)
    {
        byte highByte = (byte)(value >> 8);
        byte lowByte = (byte)(value & 0xFF);
        commands[len] = lowByte;
        commands[len + 1] = highByte;
    }

    private ushort CrcCalcMain(ushort crc, byte ch)
    {
        byte quo = (byte)((crc ^ ch) & 15);
        crc = (ushort)((crc >> 4) ^ (quo * 4225));
        quo = (byte)((crc ^ (ch >> 4)) & 15);
        crc = (ushort)((crc >> 4) ^ (quo * 4225));
        return crc;
    }

    private ushort CrcCalc(byte[] msg, int len)
    {
        ushort crc = 0;
        for (int i = 0; i < len; i++)
        {
            crc = CrcCalcMain(crc, msg[i]);
        }
        return crc;
    }

    private async Task<bool> CommandSetInhibit(bool enable = false)
    {
        commands[0] = COMMAND_HEADER;
        commands[1] = 0x05;
        commands[2] = ASCII_ACK;
        DecimalToHexa(CrcCalc(commands, 3), 3);
        await SendCommand(commands.Take((int)commands[1]).ToArray());
        return true;
    }

    private async Task<bool> GetResponse(byte command, byte expectedResponse)
    {
        int timeout = 0;
        while (timeout < TIMEOUT)
        {
            if (response.Length > 0 && response[2] == expectedResponse)
            {
                return true;
            }
            await Task.Delay(10);
            timeout += 10;
        }
        return false;
    }

    private async Task<bool> CommandStatus()
    {
        try
        {
            byte[] response = new byte[1];
            int bytesRead = await serialPort.BaseStream.ReadAsync(response, 0, 1);
            
            if (bytesRead == 1)
            {
                return response[0] == ASCII_ACK;
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error en CommandStatus: {ex.Message}");
            return false;
        }
    }

    private void CommandReset()
    {
        commands[0] = COMMAND_HEADER;
        commands[1] = 0x05;
        commands[2] = RESET;
        DecimalToHexa(CrcCalc(commands, 3), 3);
    }

    private void CommandAck()
    {
        commands[0] = COMMAND_HEADER;
        commands[1] = 0x05;
        commands[2] = ASCII_ACK;
        DecimalToHexa(CrcCalc(commands, 3), 3);
    }

    private void CommandEnable()
    {
        commands[0] = COMMAND_HEADER;
        commands[1] = 0x07;
        commands[2] = ENABLE_DENOMINATION;
        commands[3] = 0x00;
        commands[4] = 0x00;
        DecimalToHexa(CrcCalc(commands, 5), 5);
    }

    private void CommandSecurity()
    {
        commands[0] = COMMAND_HEADER;
        commands[1] = 0x07;
        commands[2] = SECURITY;
        commands[3] = 0x00;
        commands[4] = 0x00;
        DecimalToHexa(CrcCalc(commands, 5), 5);
    }

    private void CommandOptionalFunction()
    {
        commands[0] = COMMAND_HEADER;
        commands[1] = 0x07;
        commands[2] = OPTIONAL_FUNCTION;
        commands[3] = 0x00;
        commands[4] = 0x00;
        DecimalToHexa(CrcCalc(commands, 5), 5);
    }

    private void CommandSetInhibitSync(bool enableFlag)
    {
        commands[0] = COMMAND_HEADER;
        commands[1] = 0x06;
        commands[2] = INHIBIT;
        commands[3] = (byte)(enableFlag ? 0x00 : 0x01);
        DecimalToHexa(CrcCalc(commands, 4), 4);
    }

    private void CommandStack1()
    {
        commands[0] = COMMAND_HEADER;
        commands[1] = 0x05;
        commands[2] = SET_STACK_1;
        DecimalToHexa(CrcCalc(commands, 3), 3);
    }

    private async Task<bool> SetInhibit(bool enable = false)
    {
        await CommandSetInhibit(enable);
        await Task.Delay(200);
        return await GetResponse(commands[2], ASCII_ACK);
    }

    private void SetPredefinedConfiguration()
    {
        InitializePinSensorNames();
        InitializeRelayNames();
        InitializeStaticCoinValues();
        FillErrorsDispenser();
    }

    public void Dispose()
    {
        if (serialPort != null)
        {
            if (serialPort.IsOpen)
                serialPort.Close();
            serialPort.Dispose();
        }
        if (ftdiDevice != null)
        {
            ftdiDevice.Close();
        }
        GC.SuppressFinalize(this);
    }

    public void DebugMetavix(string message)
    {
        if (debugFlag)
            Console.WriteLine($"DEBUG: {message}");
    }

    public void DebugMetavixln(string message)
    {
        if (debugFlag)
        {
            Console.WriteLine($"DEBUG: {message}");
        }
    }

    private async Task<bool> ValidateCommand(byte[] cmd)
    {
        return cmd.Length >= 3 && cmd[0] == 0x02 && cmd[1] == 0x03 && cmd[2] == ASCII_ACK;
    }

    private async Task HandleCommand()
    {
        if (serialPort != null && serialPort.IsOpen && serialPort.BytesToRead > 0)
        {
            try
            {
                byte[] buffer = new byte[serialPort.BytesToRead];
                await Task.Run(() => serialPort.Read(buffer, 0, buffer.Length));
                response = buffer;
                ProcessResponse(buffer);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling command: {ex.Message}");
            }
        }
    }

    private void ProcessResponse(byte[] buffer)
    {
        // Implementar la lógica de procesamiento de respuesta
    }
}
