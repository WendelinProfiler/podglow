using WPods.Service;
int tests=0;
foreach(var item in new (bool queried,byte input,uint expected)[]{(true,1,2),(true,2,2),(true,3,3),(true,4,3),(true,0,2),(false,0,2),(false,3,2)})
{
 if(UsbSpeedPolicy.ForBluetoothRadio(item.queried,item.input)!=item.expected)throw new Exception("Invalid Bluetooth USB/IP speed");
 tests++;
}
Console.WriteLine($"PASS {tests} Bluetooth USB-speed translation checks; no low-speed bulk radio advertised");
