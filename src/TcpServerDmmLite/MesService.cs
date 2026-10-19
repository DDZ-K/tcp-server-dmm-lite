using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Xml;
using System.Windows.Forms;

namespace TcpServerDmmLite
{
    sealed class MesService
    {
        public Action<string>? Log;
        public Dictionary<string, string> dictMES = new(StringComparer.OrdinalIgnoreCase);
        public string[] paraInput = Array.Empty<string>();
        public string LastError => sError ?? "";


        public static string[] StrToStrArray(string original) =>
            original.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        public static string[] StrToStrArray(string original, char splitter) =>
            original.Split(new[] { splitter }, StringSplitOptions.RemoveEmptyEntries);
        public static string[] StrToStrArray(string original, char[] splitter) =>
            original.Split(splitter, StringSplitOptions.RemoveEmptyEntries);

        void outputToDispaly(string msg) => Log?.Invoke(msg);

public String MES_Server, scannerIP, partReceivedSent, partProcessedSent, changeoverSent, msgType;
        public int MES_Port, scannerPort;
        public TcpClient tcpClient;
        public NetworkStream stream;
        public StreamReader sr;
        public StreamWriter sw_tele;
        public XmlDocument xmlReceived;
        public Byte[] byteSent, byteReceive;
        public static  String barcodeHous, pnDevice, snDevice,sDevice, typeVar, sError;
        public Boolean bPartForStation;
        public string sStatus;
        public string telegramPath;
        public string parameterName;
        public string folderPath;
        public string[] paraMandatory;

        public void initMES(string barcode,string path, bool withSpecial = false)
        {

            if (!withSpecial)
                paraMandatory = StrToStrArray(barcode, '@');
            else
                paraMandatory = new string[] { barcode };
            barcodeHous = paraMandatory[0].Trim(new char[] { '\n', '
','\0' });

            if (paraMandatory.Length == 3)
            {
                pnDevice = paraMandatory[1];
                snDevice = paraMandatory[2];
            }
            else if (paraMandatory.Length == 2)
            {
                pnDevice = paraMandatory[1];
                snDevice = "";
            }
            else if (paraMandatory.Length == 1)
            {
                if (barcodeHous.Length == 10)
                    pnDevice = barcodeHous;
               // pnDevice = barcodeHous.Substring(barcodeHous.Length - 10);
               //  snDevice = "";
            }


            MES_Server = dictMES["server"];// '" MES_HOST_PLACEHOLDER"
            MES_Port = Int32.Parse( dictMES["port"]);

            string otherTemp=null;
            int index = path.IndexOf('\\');
            if ( index >= 0)
            {
                otherTemp = path.Trim();
                path = path.Substring(0, index).Trim();

                msgType = otherTemp.Substring(index+1, otherTemp.LastIndexOf('.')-index-1);

                if (msgType.IndexOf('_') >= 0)
                    msgType = msgType.Substring(msgType.IndexOf('_')+1);
            }


            telegramPath = dictMES["telegramPath"]+"\\"+ path.Trim(new char[] { '\n', '
' });
            tcpClient = new TcpClient();

            sr = new StreamReader("Templates\\"+ path + "\\Request_partReceived.xml");
            partReceivedSent = sr.ReadToEnd();
            sr.Close();
            sr = new StreamReader("Templates\\"+ path + "\\Request_partProcessed.xml");
            partProcessedSent = sr.ReadToEnd();
            sr.Close();

            sr = new StreamReader("Templates\\" + otherTemp);
            changeoverSent = sr.ReadToEnd();
            sr.Close();

            folderPath = "Templates\\" + path;
        }

        public void initMES(string barcode, string path, string status)
        {
            initMES(barcode, path);
            sStatus = status;
        }

        public string PartReceived_DMCnTracking()
        {
            string sentString, receivedString;

            if (!System.IO.Directory.Exists("Log_XML"))
                System.IO.Directory.CreateDirectory("Log_XML");


            if (barcodeHous.Length < 17)
            {
                sError = "Barcode " + barcodeHous + " too short";
                return "-1";
            }

            sentString = partReceivedSent.Replace("@identifier@", barcodeHous).Replace("@typeno@", pnDevice).Replace("@timeStamp@", DateTime.Now.ToString("yyyy-MM-ddThh:mm:ss.fffffff zzz"));

            try
            {
                if (!Directory.Exists(telegramPath))
                    Directory.CreateDirectory(telegramPath);

                sw_tele = new StreamWriter(telegramPath + "\\Request_partReceived_" + barcodeHous + "_" + DateTime.Now.ToString("yyMMdd_HHmmss") + ".xml");
                sw_tele.Write(sentString);
                sw_tele.Close();
            }
            catch (Exception)
            {

                sError = telegramPath + "Dir not exist or not able create";
                return "-2";
            }


            receivedString = TCP_tx_rx_MES(sentString);
            if (string.IsNullOrEmpty(receivedString))
                return "-3";


            XmlNodeList xn0, xn1, xn2;

            xmlReceived = new XmlDocument();
            xmlReceived.LoadXml(receivedString);
            //  '  xmlReceived.Load("Telegram\\Response_partReceived.xml")

            xmlReceived.Save(telegramPath + "\\Response_partReceived_" + barcodeHous + "_" + DateTime.Now.ToString("yyMMdd_HHmmss") + ".xml");
            xn0 = xmlReceived.SelectNodes("/root/event/result");
            xn1 = xmlReceived.GetElementsByTagName("workPart");
            xn2 = xmlReceived.SelectNodes("/root/body/items/item");
            if (xn0[0].Attributes["returnCode"].Value != "0")
            {
                sError = xn0[0].InnerText;
                xn0 = xmlReceived.SelectNodes("/root/event/trace/trace");

                sError += ": " + xn0[0].Attributes["text"].Value;
                bPartForStation = false;
                return "-4";
            }
            else
                sError = "";

            bPartForStation = xn1[0].Attributes["partForStation"].Value == "true" ? true : false;

            if (!bPartForStation)
                return "0";

            pnDevice = xn1[0].Attributes["typeNo"].Value;
            if (xn2.Count > 0)
            {
                snDevice = "";
                foreach (XmlNode item in xn2)
                {
                    if (item.Attributes["name"].Value == "UTSSerialNumber")
                    {
                        snDevice = item.Attributes["value"].Value;
                        break;
                    }

                }
            }

            return snDevice + pnDevice;

        }



        public bool PartReceived()
        {
            string sentString, receivedString;

            if (!System.IO.Directory.Exists("Log_XML"))
                System.IO.Directory.CreateDirectory("Log_XML");


            if (barcodeHous.Length < 17)
            {
                sError = "Barcode " + barcodeHous + " too short";
                return false;
            }

            sentString = partReceivedSent.Replace("@identifier@", barcodeHous).Replace("@typeno@", pnDevice).Replace("@timeStamp@", DateTime.Now.ToString("yyyy-MM-ddThh:mm:ss.fffffff zzz"));

            try
            {
                if (!Directory.Exists(telegramPath))
                    Directory.CreateDirectory(telegramPath);

                sw_tele = new StreamWriter(telegramPath + "\\Request_partReceived_" + barcodeHous + "_" + DateTime.Now.ToString("yyMMdd_HHmmss") + ".xml");
                sw_tele.Write(sentString);
                sw_tele.Close();
            }
            catch (Exception)
            {

                sError = telegramPath + "Dir not exist or not able create";
                return false;
            }


            receivedString = TCP_tx_rx_MES(sentString);
            if (string.IsNullOrEmpty(receivedString))
                return false;


            XmlNodeList xn0, xn1, xn2;

            xmlReceived = new XmlDocument();
            xmlReceived.LoadXml(receivedString);
            //  '  xmlReceived.Load("Telegram\\Response_partReceived.xml")

            xmlReceived.Save(telegramPath + "\\Response_partReceived_" + barcodeHous + "_" + DateTime.Now.ToString("yyMMdd_HHmmss") + ".xml");
            xn0 = xmlReceived.SelectNodes("/root/event/result");
            xn1 = xmlReceived.GetElementsByTagName("workPart");
            xn2 = xmlReceived.SelectNodes("/root/body/items/item");
            if (xn0[0].Attributes["returnCode"].Value != "0")
            {
                sError = xn0[0].InnerText;
                xn0 = xmlReceived.SelectNodes("/root/event/trace/trace");

                sError += ": " + xn0[0].Attributes["text"].Value;
                bPartForStation = false;
                return false;
            }
            else
                sError = "";

            bPartForStation = xn1[0].Attributes["partForStation"].Value == "true" ? true : false;
            pnDevice = xn1[0].Attributes["typeNo"].Value;
            if (xn2.Count > 0)
            {
                snDevice = "";
                foreach (XmlNode item in xn2)
                {
                    if (item.Attributes["name"].Value == parameterName)
                    {
                        snDevice = item.Attributes["value"].Value;
                        break;
                    }

                }

                /*
                sDevice = xn2[0].Attributes["value"].Value;
                if (sDevice.Length > 18)
                {
                    pnDevice = sDevice.Substring(0, 12);
                    snDevice = sDevice.Substring(12, 7);
                }
                else {
                    pnDevice = sDevice.Substring(0, 10);
                    snDevice = sDevice.Substring(10, 7);
                }
              */

            }

            //if there is labeling information need to be passed to next partprocessed
            XmlNodeList nodes = xmlReceived.SelectNodes("/root/body/structArrays/array/values");
            if (nodes.Count > 0)
            {
                XmlDocument xml = new XmlDocument();
                xml.Load(folderPath + "\\Request_partProcessed.xml");

                XmlNode xn;
                xn = xml.SelectSingleNode("/root/body/structArrays/array/values");

                if (xn == null)
                {
                    sError = @"/root/body/structArrays/array/values is not in PartProcessed template";
                    return false;
                }
                xn.RemoveAll();
                foreach (XmlNode item in nodes[0].ChildNodes)
                {

                    XmlElement xe = xml.CreateElement("item");
                    foreach (XmlAttribute item1 in item.Attributes)
                    {
                        xe.SetAttribute(item1.Name, item1.Value);
                    }
                    xe.InnerXml = item.InnerXml;
                    xn.AppendChild(xe);


                }

                xml.Save(folderPath + "\\Request_partProcessed.xml");

            }


            return bPartForStation;


        }

        public string PartReceivedOnlyDMC()
        {
            string sentString, receivedString;

            if (!System.IO.Directory.Exists("Log_XML"))
                System.IO.Directory.CreateDirectory("Log_XML");


            if (barcodeHous.Length < 17)
            {
                sError = "Barcode "+barcodeHous+" too short";
                return "0";
            }

            sentString = partReceivedSent.Replace("@identifier@", barcodeHous).Replace ("@typeno@", pnDevice).Replace("@timeStamp@", DateTime.Now.ToString("yyyy-MM-ddThh:mm:ss.fffffff zzz"));

            try
            {
                if (!Directory.Exists(telegramPath))
                    Directory.CreateDirectory(telegramPath);

                sw_tele = new StreamWriter(telegramPath + "\\Request_partReceived_" + barcodeHous + "_" + DateTime.Now.ToString("yyMMdd_HHmmss") + ".xml");
                sw_tele.Write(sentString);
                sw_tele.Close();
            }
            catch (Exception)
            {

                sError = telegramPath+"Dir not exist or not able create";
                    return "0";
            }


            receivedString = TCP_tx_rx_MES(sentString);
            if (string.IsNullOrEmpty(receivedString))
                return "0";


            XmlNodeList xn0, xn1,xn2;

            xmlReceived = new XmlDocument();
            xmlReceived.LoadXml(receivedString);
            //  '  xmlReceived.Load("Telegram\\Response_partReceived.xml")

            xmlReceived.Save(telegramPath +  "\\Response_partReceived_" + barcodeHous + "_" + DateTime.Now.ToString("yyMMdd_HHmmss") + ".xml");
            xn0 = xmlReceived.SelectNodes("/root/event/result");
            xn1 = xmlReceived.GetElementsByTagName("workPart");
            xn2 = xmlReceived.SelectNodes("/root/body/items/item");

            if (xn1.Count == 0)
                return "";

            pnDevice = xn1[0].Attributes["typeNo"].Value;
            if (xn2.Count > 0)
            {
                snDevice = "";
                foreach (XmlNode  item in xn2)
                {
                    if (item.Attributes["name"].Value == "UTSSerialNumber")
                    {
                        snDevice = item.Attributes["value"].Value;
                        break;
                    }

                }
            }




            return snDevice+pnDevice;


        }

        public string PartReceivedParameter(string parameter,string attr)
        {
            string sentString, receivedString;

            if (!System.IO.Directory.Exists("Log_XML"))
                System.IO.Directory.CreateDirectory("Log_XML");


            if (barcodeHous.Length < 17)
            {
                sError = "Barcode " + barcodeHous + " too short";
                return "0";
            }

            sentString = partReceivedSent.Replace("@identifier@", barcodeHous).Replace("@typeno@", pnDevice).Replace("@timeStamp@", DateTime.Now.ToString("yyyy-MM-ddThh:mm:ss.fffffff zzz"));

            try
            {
                if (!Directory.Exists(telegramPath))
                    Directory.CreateDirectory(telegramPath);

                sw_tele = new StreamWriter(telegramPath + "\\Request_partReceived_" + barcodeHous + "_" + DateTime.Now.ToString("yyMMdd_HHmmss") + ".xml");
                sw_tele.Write(sentString);
                sw_tele.Close();
            }
            catch (Exception)
            {

                sError = telegramPath + "Dir not exist or not able create";
                return "0";
            }


            receivedString = TCP_tx_rx_MES(sentString);
            if (string.IsNullOrEmpty(receivedString))
                return "0";


            XmlNodeList xn0, xn1, xn2;

            xmlReceived = new XmlDocument();
            xmlReceived.LoadXml(receivedString);
            //  '  xmlReceived.Load("Telegram\\Response_partReceived.xml")

            xmlReceived.Save(telegramPath + "\\Response_partReceived_" + barcodeHous + "_" + DateTime.Now.ToString("yyMMdd_HHmmss") + ".xml");
            xn0 = xmlReceived.SelectNodes(parameter);

            if (xn0.Count == 0)
                return "";
            else
             return xn0[0].Attributes[attr].Value;


        }


        public string Spe_PCBAtoHousingWithTracking(string para)
        {
            paraInput = StrToStrArray(para, '|');

            initMES(paraInput[0], paraInput[1]);
            string[] subpara = StrToStrArray(paraInput[2]);
            string housing = PartReceivedParameterWithTracking(subpara[0], subpara[1]);
            if (string.IsNullOrEmpty(housing))
                return "0|-2";
            else
                return "1|" + housing;
        }


        public string PartReceivedParameterWithTracking(string parameter, string attr)
        {
            string sentString, receivedString;

            if (!System.IO.Directory.Exists("Log_XML"))
                System.IO.Directory.CreateDirectory("Log_XML");


            if (barcodeHous.Length < 17)
            {
                sError = "Barcode " + barcodeHous + " too short";
                return "0";
            }

            sentString = partReceivedSent.Replace("@identifier@", barcodeHous).Replace("@typeno@", pnDevice).Replace("@timeStamp@", DateTime.Now.ToString("yyyy-MM-ddThh:mm:ss.fffffff zzz"));

            try
            {
                if (!Directory.Exists(telegramPath))
                    Directory.CreateDirectory(telegramPath);

                sw_tele = new StreamWriter(telegramPath + "\\Request_partReceived_" + barcodeHous + "_" + DateTime.Now.ToString("yyMMdd_HHmmss") + ".xml");
                sw_tele.Write(sentString);
                sw_tele.Close();
            }
            catch (Exception)
            {

                sError = telegramPath + "Dir not exist or not able create";
                return "0";
            }


            receivedString = TCP_tx_rx_MES(sentString);
            if (string.IsNullOrEmpty(receivedString))
                return "0";


            XmlNodeList xn0, xn1, xn2;

            xmlReceived = new XmlDocument();
            xmlReceived.LoadXml(receivedString);
            //  '  xmlReceived.Load("Telegram\\Response_partReceived.xml")

            xmlReceived.Save(telegramPath + "\\Response_partReceived_" + barcodeHous + "_" + DateTime.Now.ToString("yyMMdd_HHmmss") + ".xml");
            xn0 = xmlReceived.SelectNodes(parameter);



            if (xn0.Count == 0)
                return "";
            else
            {
                if (xn0[0].Attributes["partForStation"].Value.ToLower() == "true")
                    return xn0[0].Attributes[attr].Value;
                else
                    return "0";
            }



        }

        public void testXML()
        {


            xmlReceived = new XmlDocument();
            xmlReceived.Load("Templates\\WAVE3_FCT50\\Request_partProcessed.xml");
            XmlNode xn0;
            XmlElement ele;
            xn0 = xmlReceived.SelectSingleNode("/root/body/structArrays/array/values");
            ele = xmlReceived.CreateElement("item");
            ele.SetAttribute("field", "field01");
            ele.SetAttribute("name", "name01");
            ele.SetAttribute("value", "value01");
            xn0.AppendChild(ele);

      string ss  =      xmlReceived.InnerXml;

            xmlReceived.Save("d:\\test.xml");



            return ;
        }

        public int PartProcessed()
        {
            string sentString, receivedString, returnCode;

            sentString = partProcessedSent;
            sentString = sentString.Replace("@identifier@", barcodeHous).
               Replace("@typeno@", pnDevice).Replace("@SerialNumber@", snDevice).
               Replace("@result@", (sStatus)).Replace("@timeStamp@", DateTime.Now.ToString("yyyy-MM-ddThh:mm:ss.fffffff zzz"));

            if (!Directory.Exists(telegramPath))
                Directory.CreateDirectory(telegramPath);

            sw_tele = new StreamWriter(telegramPath + "\\Request_partProcessed_" + barcodeHous + "_" + DateTime.Now.ToString("yyMMdd_HHmmss") + ".xml");
            sw_tele.Write(sentString);
            sw_tele.Close();
            receivedString = TCP_tx_rx_MES(sentString);
            if (string.IsNullOrEmpty(receivedString))
                return -1;


            XmlNodeList xn1;
            xmlReceived = new XmlDocument();
            xmlReceived.LoadXml(receivedString);

            xmlReceived.Save(telegramPath + "\\Response_partProcessed_" + barcodeHous + "_" + DateTime.Now.ToString("yyMMdd_HHmmss") + ".xml");

            xn1 = xmlReceived.SelectNodes("/root/event/result");

            returnCode = xn1[0].Attributes["returnCode"].Value;

            if (returnCode != "0")
            {
                sError = xn1[0].InnerText;
                xn1 = xmlReceived.SelectNodes("/root/event/trace/trace");
                sError += " " + xn1[0].Attributes["text"].Value;

            }

            return Int16.Parse(returnCode);
        }

        public int PartProcessed( string name, string value)
        {
            string sentString, receivedString, returnCode;
            string[] sName = StrToStrArray(name, '@');
            string[] sValue = StrToStrArray(value, '@');

            if ( sName.Length  != sValue.Length)
            {
                sError = "name qty differ from values";
                return -1;
            }

            sentString = partProcessedSent;


            for (int i = 0; i < sName.Length; i++)
            {
                sentString = sentString.Replace("@"+sName[i]+"@",sValue [i]);
            }

            sentString = sentString.Replace("@identifier@", barcodeHous).
              Replace("@typeno@", pnDevice).Replace("@SerialNumber@", snDevice).
              Replace("@result@", (sStatus)).Replace("@timeStamp@", DateTime.Now.ToString("yyyy-MM-ddThh:mm:ss.fffffff zzz"));

            if (!Directory.Exists(telegramPath))
                Directory.CreateDirectory(telegramPath);

            sw_tele = new StreamWriter(telegramPath + "\\Request_partProcessed_" + barcodeHous + "_" + DateTime.Now.ToString("yyMMdd_HHmmss") + ".xml");
            sw_tele.Write(sentString);
            sw_tele.Close();
            receivedString = TCP_tx_rx_MES(sentString);
            if (string.IsNullOrEmpty(receivedString))
                return -1;


            XmlNodeList xn1;
            xmlReceived = new XmlDocument();
            xmlReceived.LoadXml(receivedString);

            xmlReceived.Save(telegramPath + "\\Response_partProcessed_" + barcodeHous + "_" + DateTime.Now.ToString("yyMMdd_HHmmss") + ".xml");

            xn1 = xmlReceived.SelectNodes("/root/event/result");

            returnCode = xn1[0].Attributes["returnCode"].Value;

            if (returnCode != "0")
            {
                sError = xn1[0].InnerText;
                xn1 = xmlReceived.SelectNodes("/root/event/trace/trace");
                sError += " " + xn1[0].Attributes["text"].Value;

            }

            return Int16.Parse(returnCode);
        }


        /*
        public int PartProcessed(string field, string name, string value)
        {
            string sentString, receivedString, returnCode;

            string[] sField = StrToStrArray(field, '@');
            string[] sName = StrToStrArray(name, '@');
            string[] sValue = StrToStrArray(value, '@');

            if (sField.Length != sName.Length || sField.Length != sValue.Length)
            {
                sError = "idpalte parameter qty differ from each other";
                return -1;
            }

            sentString = partProcessedSent;
            sentString = sentString.Replace("@identifier@", barcodeHous).
               Replace("@typeno@", pnDevice).Replace("@SerialNumber@", snDevice).
               Replace("@result@", (sStatus)).Replace ("@timeStamp@", DateTime.Now.ToString("yyyy-MM-ddThh:mm:ss.fffffff zzz"));

            xmlReceived = new XmlDocument();
            xmlReceived.LoadXml(sentString);
            XmlNode xn0;
            XmlElement ele;
            xn0 = xmlReceived.SelectSingleNode ("/root/body/structArrays/array/values");


            for (int i = 0; i < sField.Length ; i++)
            {
                ele = xmlReceived.CreateElement("item");
                ele.SetAttribute("field", sField[i]);
                ele.SetAttribute("name", sName[i]);
                ele.SetAttribute("value", sValue[i]);

                for (int j = 0; j < xn0.ChildNodes.Count; j++)
                {
                    if (xn0.ChildNodes[j].Attributes["field"].Value == sField[i])
                    {
                        xn0.RemoveChild(xn0.ChildNodes[j]);
                    }
                }

                xn0.AppendChild(ele);
            }


            if (!Directory.Exists(telegramPath))
                Directory.CreateDirectory(telegramPath);

            xmlReceived.Save (telegramPath + "\\Request_partProcessed_" + barcodeHous + "_" + DateTime.Now.ToString("yyMMdd_HHmmss") + ".xml");

            receivedString = TCP_tx_rx_MES(xmlReceived.InnerXml );
            if (string.IsNullOrEmpty(receivedString))
                return -1;


            XmlNodeList xn1;
            xmlReceived = new XmlDocument();
            xmlReceived.LoadXml(receivedString);

            xmlReceived.Save(telegramPath + "\\Response_partProcessed_" + barcodeHous + "_" + DateTime.Now.ToString("yyMMdd_HHmmss") + ".xml");

            xn1 = xmlReceived.SelectNodes("/root/event/result");

            returnCode = xn1[0].Attributes["returnCode"].Value;

            if (returnCode != "0")
            {
                sError = xn1[0].InnerText;
                xn1 = xmlReceived.SelectNodes("/root/event/trace/trace");
                sError += " " + xn1[0].Attributes["text"].Value;

            }

            return Int16.Parse(returnCode);
        }
        */
        public int PartProcessed(string field, string name, string value)
        {
            string sentString, receivedString, returnCode;

            string[] sField = StrToStrArray(field, '@');
            string[] sName = StrToStrArray(name, '@');
            string[] sValue = StrToStrArray(value, '@');

            if (sValue.Length != sName.Length || sValue.Length < sField.Length)
            {
                sError = "idpalte parameter qty differ from each other";
                return -1;
            }

            sentString = partProcessedSent;
            sentString = sentString.Replace("@identifier@", barcodeHous).
               Replace("@typeno@", pnDevice).Replace("@SerialNumber@", snDevice).
               Replace("@result@", (sStatus)).Replace("@timeStamp@", DateTime.Now.ToString("yyyy-MM-ddThh:mm:ss.fffffff zzz"));

            //for parameter without field
            for (int i = sField.Length; i < sValue.Length; i++)
            {
                sentString = sentString.Replace("@" + sName[i] + "@", sValue[i]);
            }


            xmlReceived = new XmlDocument();
            xmlReceived.LoadXml(sentString);
            XmlNode xn0;
            XmlElement ele;
            xn0 = xmlReceived.SelectSingleNode("/root/body/structArrays/array/values");


            for (int i = 0; i < sField.Length; i++)
            {
                ele = xmlReceived.CreateElement("item");
                ele.SetAttribute("field", sField[i]);
                ele.SetAttribute("name", sName[i]);
                ele.SetAttribute("value", sValue[i]);

                for (int j = 0; j < xn0.ChildNodes.Count; j++)
                {
                    if (xn0.ChildNodes[j].Attributes["field"].Value == sField[i])
                    {
                        xn0.RemoveChild(xn0.ChildNodes[j]);
                    }
                }

                xn0.AppendChild(ele);
            }





            if (!Directory.Exists(telegramPath))
                Directory.CreateDirectory(telegramPath);

            xmlReceived.Save(telegramPath + "\\Request_partProcessed_" + barcodeHous + "_" + DateTime.Now.ToString("yyMMdd_HHmmss") + ".xml");

            receivedString = TCP_tx_rx_MES(xmlReceived.InnerXml);
            if (string.IsNullOrEmpty(receivedString))
                return -1;


            XmlNodeList xn1;
            xmlReceived = new XmlDocument();
            xmlReceived.LoadXml(receivedString);

            xmlReceived.Save(telegramPath + "\\Response_partProcessed_" + barcodeHous + "_" + DateTime.Now.ToString("yyMMdd_HHmmss") + ".xml");

            xn1 = xmlReceived.SelectNodes("/root/event/result");

            returnCode = xn1[0].Attributes["returnCode"].Value;

            if (returnCode != "0")
            {
                sError = xn1[0].InnerText;
                xn1 = xmlReceived.SelectNodes("/root/event/trace/trace");
                sError += " " + xn1[0].Attributes["text"].Value;

            }

            return Int16.Parse(returnCode);
        }

        public string TCP_tx_rx_MES(string sentString)
        {
            int bytesNum;
            byteReceive = new byte[65536];
            byteSent = new byte[sentString.Length + 4];
            byteSent[0] = (byte)((sentString.Length + 4) / (256 * 256 * 256));
            byteSent[1] = (byte)((sentString.Length + 4) / (256 * 256));
            byteSent[2] = (byte)((sentString.Length + 4) / 256);
            byteSent[3] = (byte)((sentString.Length + 4) % 256);
            Encoding.UTF8.GetBytes(sentString).CopyTo(byteSent, 4);

            try
            {
                if (!tcpClient.Connected)
                {
                    tcpClient = new TcpClient();
                    tcpClient.Connect(MES_Server, MES_Port);
                }

            }
            catch (Exception)
            {
                sError = "Unable to connect to MES:" + MES_Server;
                return "";
            }

            try
            {
                stream = tcpClient.GetStream();
                stream.Write(byteSent, 0, byteSent.Length);

                stream.ReadTimeout = 5000;
                bytesNum = stream.Read(byteReceive, 0, tcpClient.ReceiveBufferSize);
                stream.Close();
                tcpClient.Close();
            }
            catch (Exception)
            {
                sError = "Unable to get telegram from MES:" + MES_Server;
                return "";
            }


            if (bytesNum <= 4)
            {
                sError = "receive empty from server";
                stream.Close();
                tcpClient.Close();
                return "";
            }

            sError = "";
            return Encoding.ASCII.GetString(byteReceive, 4, bytesNum - 4);
        }

        public string Spe_PartProcessed(string barcode)
        {
            //1. barcode 2.folder 3.status

            string[] ss = StrToStrArray(barcode, '|');
            initMES(ss[0], ss[1], ss[2]);

            int ret;
            if (ss.Length == 3)
                ret = PartProcessed();
            else if (ss.Length == 5)   //find name = value and replace
                ret = PartProcessed(ss[3], ss[4]);
            else if (ss.Length == 6)
                ret = PartProcessed(ss[3], ss[4], ss[5]);
            else
                return "0|missing parameters";

            if (ret == 0)
                return "1|0";
            else
                return  "0|return code:" + ret.ToString() +" with error info: "+ sError;

        }

        public string Spe_PartReceived(string barcode)
        {

            string[] para = StrToStrArray(barcode, '|');

            initMES(para[0], para[1]);

            if (para.Length > 2)
            {
                parameterName = para[2];
            }
            else
                parameterName = "serialNumber";

            if (PartReceived())
                return "1|1";


            if (string.IsNullOrEmpty(sError))
                return "1|0";
            else
                return  "0|"+ sError;

        }

        public string Spe_PartReceivedwithDMC(string barcode)   //return pnDevice +snDevice
        {

            string[] para = StrToStrArray(barcode, '|');

            initMES(para[0], para[1]);

            if (para.Length > 2)
            {
                parameterName = para[2];
            }
            else
                parameterName = "serialNumber";

            if (PartReceived())
                return "1|"+ pnDevice +snDevice  ;


            if (string.IsNullOrEmpty(sError))
                return "1|0";
            else
                return "0|" + sError;

        }

        public string Spe_PartReceivedOnlyDMC(string barcode)   //return pnDevice +snDevice
        {

            string[] para = StrToStrArray(barcode, '|');

            initMES(para[0], para[1]);

            if (para.Length > 2)
            {
                parameterName = para[2];
            }
            else
                parameterName = "serialNumber";


                return "1|" + PartReceivedOnlyDMC();

        }

        public string Spe_PartReceivedParameter(string barcode)   //return pnDevice +snDevice
        {

            string[] para = StrToStrArray(barcode, '|');

            initMES(para[0], para[1]);

            if (para.Length > 2)
            {
                parameterName = para[2];
            }


            string[] subpara = StrToStrArray(parameterName);
            return "1|" + PartReceivedParameter(subpara[0], subpara[1]);

        }

        public string Spe_PCBAtoDevice(string para)
        {
            paraInput = StrToStrArray(para, '|');

            if (paraInput.Length < 4)
                return "0|-1";

            initMES(paraInput[0], paraInput[1]);

            string[] subpara = StrToStrArray(paraInput[2]);

            string housing = PartReceivedParameter(subpara[0], subpara[1]);

            if (string.IsNullOrEmpty(housing))
                return "0|-2";

            initMES(housing, paraInput[3]);

            string device = PartReceivedOnlyDMC();

            if (string.IsNullOrEmpty(device))
                return "0|-3";
            else
                return "1|" + device;
        }

        public string Spe_PCBAtoDeviceWithTracking(string para)
        {
            paraInput = StrToStrArray(para, '|');

            if (paraInput.Length < 4)
                return "0|-1";

            initMES(paraInput[0], paraInput[1]);

            string[] subpara = StrToStrArray(paraInput[2]);

            string housing = PartReceivedParameter(subpara[0], subpara[1]);

            if (string.IsNullOrEmpty(housing))
                return "0|-2";

            initMES(housing, paraInput[3]);

            string device = PartReceived_DMCnTracking();

            if (device.Length<3)
                return "0|" + device;
            else
                return "1|" + device;
        }

        public string Spe_PCBAtoHousing(string para)
        {
            paraInput = StrToStrArray(para, '|');

            initMES(paraInput[0], paraInput[1]);
            string[] subpara = StrToStrArray(paraInput[2]);
            string housing = PartReceivedParameter(subpara[0], subpara[1]);
            if (string.IsNullOrEmpty(housing))
                return "0|-2";
            else
                return "1|" + housing;
        }

        public string Spe_PartReceived_DMCnTracking(string para)
        {
            paraInput = StrToStrArray(para, '|');
            initMES(paraInput[0], paraInput[1]);
            string device = PartReceived_DMCnTracking();

            if (device.Length < 3)
                return "0|" + device;
            else
                return "1|" + device;
        }


        // MultiToSingle 保留独立 MES 上报；依赖 PPV 的 Multi 入口已删除。




        public string Spe_PartProcessed_MultiToSingle(string para)
        {
            //1. barcode 2.folder; status
            //same as Spe_PartReceived_Multi return value:  DMC:s1-32;pos:s33;partForStation:s34(1 for true, 0 for false)
            string[] ss = StrToStrArray(para, '|');
            string barcode = ss[0];
            string pos1 = ss[0].Substring(15, 1);

            initMES(barcode, ss[1], ss[2]);

            int ret;
            ret = PartProcessed_MultiToSingle(barcode, ss[2]);

            if (ret == 0)
            {
                return "1|0";
            }

            else
                return "0|return code:" + ret.ToString() + " with error info: " + sError;

        }

        public int PartProcessed_MultiToSingle(string barcode, string result)
        {
            string sentString, receivedString, returnCode, barcode1;

            sentString = partProcessedSent;
            sentString = sentString.Replace("@identifier@", barcodeHous).
               Replace("@typeno@", pnDevice).Replace("@SerialNumber@", snDevice).
               Replace("@result@", (sStatus)).Replace("@timeStamp@", DateTime.Now.ToString("yyyy-MM-ddThh:mm:ss.fffffff zzz"));

            barcode1 = barcode.Remove(15, 1).Insert(15, "1");
            sentString = sentString.Replace("@id_1@", barcode1).Replace("@id_1_result@", result).Replace("@id_1_pos@", "1");

            xmlReceived = new XmlDocument();
            xmlReceived.LoadXml(sentString);
            if (!Directory.Exists(telegramPath))
                Directory.CreateDirectory(telegramPath);

            xmlReceived.Save(telegramPath + "\\Request_partProcessed_" + barcodeHous + "_" + DateTime.Now.ToString("yyMMdd_HHmmss") + ".xml");

            receivedString = TCP_tx_rx_MES(xmlReceived.InnerXml);
            if (string.IsNullOrEmpty(receivedString))
                return -1;

            XmlNodeList xn1;
            xmlReceived = new XmlDocument();
            xmlReceived.LoadXml(receivedString);

            xmlReceived.Save(telegramPath + "\\Response_partProcessed_" + barcodeHous + "_" + DateTime.Now.ToString("yyMMdd_HHmmss") + ".xml");

            xn1 = xmlReceived.SelectNodes("/root/event/result");

            returnCode = xn1[0].Attributes["returnCode"].Value;

            if (returnCode != "0")
            {
                sError = xn1[0].InnerText;
                xn1 = xmlReceived.SelectNodes("/root/event/trace/trace");
                sError += " " + xn1[0].Attributes["text"].Value;

            }

            return Int16.Parse(returnCode);
        }


        public string Spe_MES_Init(string para)
        {
            paraInput = StrToStrArray(para, '|');
            dictMES["server"] =paraInput[0] ;
            dictMES["port"] = paraInput[1];
            return "1|1";

        }


        public string Spe_ChangeOver(string para)
        {
            paraInput = StrToStrArray(para, '|');
            string barcode = paraInput[0];
            initMES(barcode, paraInput[1],true);
            string sentString, receivedString, returnCode;
            sentString = changeoverSent;
            sentString = sentString.Replace("@identifier@", barcodeHous).
               Replace("@typeno@", pnDevice).Replace("@SerialNumber@", snDevice).
               Replace("@result@", (sStatus)).Replace("@timeStamp@", DateTime.Now.ToString("yyyy-MM-ddThh:mm:ss.fffffff zzz"));

            xmlReceived = new XmlDocument();
            xmlReceived.LoadXml(sentString);
            if (!Directory.Exists(telegramPath))
                Directory.CreateDirectory(telegramPath);

            xmlReceived.Save(telegramPath + "\\Request_"+ msgType +"_" + barcodeHous + "_" + DateTime.Now.ToString("yyMMdd_HHmmss") + ".xml");

            receivedString = TCP_tx_rx_MES(xmlReceived.InnerXml);
            if (string.IsNullOrEmpty(receivedString))
                return "0|-1";

            XmlNodeList xn1;
            xmlReceived = new XmlDocument();
            xmlReceived.LoadXml(receivedString);

            xmlReceived.Save(telegramPath + "\\Response_"+msgType+"_"+barcodeHous + "_" + DateTime.Now.ToString("yyMMdd_HHmmss") + ".xml");

            xn1 = xmlReceived.SelectNodes("/root/event/result");

            returnCode = xn1[0].Attributes["returnCode"].Value;

            if (returnCode != "0")
            {
                sError = xn1[0].InnerText;
                xn1 = xmlReceived.SelectNodes("/root/event/trace/trace");
                sError += " " + xn1[0].Attributes["text"].Value;
            }

            return "1|"+returnCode;
        }

        public string Spe_MaterialChangeStarted(string para)
        {
            paraInput = StrToStrArray(para, '|');
            string barcode = paraInput[0];
            initMES(barcode, paraInput[1],true);
            string sentString, receivedString, returnCode;
            sentString = changeoverSent;
            sentString = sentString.Replace("@identifier@", barcodeHous).
               Replace("@typeno@", pnDevice).Replace("@SerialNumber@", snDevice).
               Replace("@result@", (sStatus)).Replace("@timeStamp@", DateTime.Now.ToString("yyyy-MM-ddThh:mm:ss.fffffffzzz"));

            xmlReceived = new XmlDocument();
            xmlReceived.LoadXml(sentString);
            if (!Directory.Exists(telegramPath))
                Directory.CreateDirectory(telegramPath);

            xmlReceived.Save(telegramPath + "\\Request_" + msgType + "_" + DateTime.Now.ToString("yyMMdd_HHmmss") + ".xml");

            receivedString = TCP_tx_rx_MES(xmlReceived.InnerXml);
            if (string.IsNullOrEmpty(receivedString))
                return "0|-1";

            XmlNodeList xn1,xn2;
            xmlReceived = new XmlDocument();
            xmlReceived.LoadXml(receivedString);

            xmlReceived.Save(telegramPath + "\\Response_" + msgType + "_" + DateTime.Now.ToString("yyMMdd_HHmmss") + ".xml");

            xn1 = xmlReceived.SelectNodes("/root/event/result");

            returnCode = xn1[0].Attributes["returnCode"].Value;

            if (returnCode != "0")
            {
                sError = xn1[0].InnerText;
                xn1 = xmlReceived.SelectNodes("/root/event/trace/trace");
                sError += " " + xn1[0].Attributes["text"].Value;
            }

            xn2 = xmlReceived.SelectNodes("/root/body/structArrays/array/values/item");

            if (xn2!=null&&xn2.Count > 0)
            {
                if (!Directory.Exists("C:\\uts"))
                    Directory.CreateDirectory("C:\\uts");
                StreamWriter sw = new StreamWriter("C:\\uts\\mes_log.ini");
                sw.Write("[" + msgType + "]");

                foreach (XmlNode item in xn2)
                {
                    foreach (System.Xml.XmlAttribute item1 in item.Attributes)
                    {
                        sw.Write('\n' + item1.Name + "=" + item1.Value);
                    }
                }

                sw.Close();
            }


            return "1|" + returnCode;
        }

    }
}
