const int AnuncioParada = 4;
const int AnuncioPrecaucion = 5;
const int PreanuncioParada = 6;
const int PaN = 7;
const int LTV = 2;
const int Rebase = 3;
const int AumVel = 8;
const int Modo = 10;
const int Rearme = 9;
const int Ocultacion = 0;
const int Alarma = 0;
const int ButtonsNumber = 9;
unsigned long PreviousTime;
enum ASFA_Info
{
  Via_libre,
  Via_libre_condicional,
  Anuncio_parada,
  Anuncio_precaucion,
  Preanuncio_parada,
  Preanuncio_parada_AV,
  Previa_rojo,
  Parada,
  Desconocido
};
class Button
{
  public:
    int PressedPort;
    int OnPort;
    unsigned long Time_pressed;
    bool IsPressed;
    bool WasPressed;
    bool IsOn;
    bool WasOn;
    Button() {
      IsOn = true;
    }
    void Update(unsigned long elapsedTime)
    {
      if (IsPressed && IsOn && WasPressed && WasOn)
      {
        Time_pressed += elapsedTime;
      }
      else Time_pressed = 0;
      WasPressed = IsPressed;
      WasOn = IsOn;
    }
};
class ASFA_State
{
  public:
    Button Buttons[ButtonsNumber];
    /*ASFA_Info UltimaInfo;
    int Speed;
    int Target;
    bool Overspeed1;
    bool Overspeed2;
    bool Emergency;*/
} ASFAState;
void setup() {
  // put your setup code here, to run once:
  for (int i = 0; i < ButtonsNumber; i++)
  {
    int port;
    switch(i)
    {
      case 0:
        port = AnuncioParada;
        break;
      case 1:
        port = AnuncioPrecaucion;
        break;
      case 2:
        port = PreanuncioParada;
        break;
      case 3:
        port = PaN;
        break;
      case 4:
        port = LTV;
        break;
      case 5:
        port = Rebase;
        break;
      case 6:
        port = AumVel;
        break;
      case 7:
        port = Modo;
        break;
      case 8:
        port = Rearme;
        break;
    }
    ASFAState.Buttons[i].PressedPort = port;
    pinMode(port, INPUT);
  }
  /*pinMode(11, OUTPUT);
  pinMode(12, OUTPUT);
  pinMode(13, OUTPUT);*/
  Serial.begin(9600);
  while (!Serial) {}
  PreviousTime = 0;
}
int WasAvailable = 1;
long BlinkingTime = 0;
int GreenLedState = LOW;
unsigned long PreviousSend = 0; 
ASFA_State prevstate;
void loop() {
  // put your main code here, to run repeatedly:
  /*char info[] = {0, 0, 0, 0, 0, 0, 0};
  byte buff[5];
  if(Serial.available()>=7)
  {
    if(Serial.peek()>='0'&&Serial.peek()<='9') Serial.readBytes(info, 7);
    if(info[6]!='\n')
    {
      while((Serial.read()!='\n'&&Serial.peek()<='0'&&Serial.peek()>='9')||Serial.available());
    }
    else if(info[1]=='A')
    {
      ASFAState.UltimaInfo = (ASFA_Info)(((int)info[0])-48);
    }
  }
  ASFAState.Speed = info[1];
  ASFAState.Target = info[2];*/
  for (int i = 0; i < ButtonsNumber; i++)
  {
    ASFAState.Buttons[i].IsPressed = digitalRead(ASFAState.Buttons[i].PressedPort);
    ASFAState.Buttons[i].Update(millis() - PreviousTime);
	if(abs(ASFAState.Buttons[i].TimePressed - prevState.Buttons[i].TimePressed)>250)
	{
		Serial.write(i);
		Serial.write(ASFAState.Buttons[i].TimePressed / 20);
		Serial.write(255);
		Serial.write(255);
	}
  }
  /*switch (ASFAState.UltimaInfo)
  {
    case Via_libre:
      digitalWrite(13, HIGH);
      digitalWrite(12, LOW);
      digitalWrite(11, LOW);
      break;
    case Via_libre_condicional:
      if (millis() - BlinkingTime > 500)
      {
        GreenLedState = GreenLedState == LOW ? HIGH : LOW;
        digitalWrite(13, GreenLedState);
        BlinkingTime = millis();
      }
      digitalWrite(12, LOW);
      digitalWrite(11, LOW);
      break;
    case Anuncio_parada:
      digitalWrite(13, LOW);
      digitalWrite(12, LOW);
      digitalWrite(11, HIGH);
      break;
    case Anuncio_precaucion:
      digitalWrite(13, HIGH);
      digitalWrite(12, LOW);
      digitalWrite(11, HIGH);
      break;
    case Previa_rojo:
    case Parada:
      digitalWrite(13, LOW);
      digitalWrite(12, HIGH);
      digitalWrite(11, LOW);
      break;
    case Desconocido:
      digitalWrite(13, LOW);
      digitalWrite(12, LOW);
      digitalWrite(11, LOW);
      break;
  }*/
  PreviousTime = millis();
}
