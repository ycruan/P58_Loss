﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace P58_Loss.GlobalLib
{
    public struct ConstSet
    {
        public static readonly double FeetToMeter = 0.3048;
    }

    public class MyLevel
    {
        private static List<double> _elevations;
        private static List<double> _elevations_adj = null;
        private static int _num;
        private static MyLevel _myLevel = null;

        private MyLevel(FilteredElementCollector levelColls)
        {
            _num = 0;
            _elevations = new List<double>();

            foreach (Level level in levelColls)
            {
                _elevations.Add(level.Elevation);
                ++_num;
            }

            _elevations.Sort();

        }
        public static void SetMyLevel(FilteredElementCollector levelColls)
        {
            if (_myLevel == null) _myLevel = new MyLevel(levelColls);
        }
        public static MyLevel GetMyLevel()
        {
            return _myLevel;
        }
        public int GetFloor(out bool isFound, Level oriLevel, double offset = 0)  //Return the next larger item if not found.
        {
            int rank = 0;
            double ela = oriLevel.Elevation + offset;
            if(_elevations_adj != null)
                rank = _elevations_adj.BinarySearch(ela);
            else
                rank = _elevations.BinarySearch(ela);
            if (rank < 0)
            {
                rank = ~rank;                   //C# ref: return the bitwise complement of the next larger item if not found
                isFound = false;
            }
            else
            {
                isFound = true;
            }
            return rank;
        }
        public double GetElavation(int floor)
        {
            return (_elevations_adj != null? _elevations_adj.ElementAt(floor) : _elevations.ElementAt(floor));
        }
        public static int GetLevelNum()         //Note: num_floor = num_level - 1
        {
            if (_myLevel != null) return _num;
            else
            {
                throw (new Exception("Error in: P58_Loss.GlobalLib.MyLevel.GetLevelNum()\t\tMyLevel not yet instantiated"));
            }
        }
        public static void WriteLevelsToInFile(ref string inFile)
        {
            foreach(double ela in _elevations)
            {
                inFile += (ela * ConstSet.FeetToMeter).ToString() + "\t";
            }
            inFile += "\r\n";
        }
        public static void AdjustLevels(AdditionalInfo addiInfo)
        {
            _elevations_adj = new List<double>(_elevations.Count);
            for (int i = 0; i < _num; ++i)
            {
                if (addiInfo.unCheckedLevel[i] == 0)
                {
                    _elevations_adj.Add(_elevations[i]);
                }            
            }
            _num = _elevations_adj.Count;
        }
    }

    public class ErrorWriter
    {
        private static string _directory = System.AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "PGCreator";
        private static string _fileName = @"\ErrorLog.log";
        private static ErrorWriter _errorWriter = null;
        private static string _error = DateTime.Now.ToString() + "\r\n";
        private ErrorWriter(string dir = null)
        {
            if (dir != null) _directory = dir;
        }
        public static void SetWriter(string dir = null)
        {
            if (_errorWriter == null) _errorWriter = new ErrorWriter(dir);
        }
        public static ErrorWriter GetWriter()
        {
            return _errorWriter;
        }
        public void WriteError(Exception e)
        {
            _error += e.ToString();
        }
        public void WriteError(string e)
        {
            _error += e;
        }
        public static void Output()
        {
            FileStream fs = new FileStream(_directory + _fileName, FileMode.Create);
            StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.Default);
            sw.Write(_error);
            sw.Flush();
            sw.Close();
            fs.Close();
        }
    }

    public class PGItem
    {
        public string PGName;
        public string Code;
        public Direction direction;
        public bool IfDefinePrice;
        public string PinYinSuffix;
        public double Price;            //USD
        public List<double> Num;        //May not be int
        private static int _num_floor;

        static PGItem()
        {
            _num_floor = MyLevel.GetLevelNum() - 1;
        }
        public PGItem()
        {
            PGName = "\t";
            Code = "\t";
            IfDefinePrice = false;
            PinYinSuffix = "\t";
            Price = 0.0;
            Num = new List<double>(_num_floor);
            for (int i = 0; i < _num_floor; ++i)
            {
                Num.Add(0.0);
            }
        }
    }

    public class PGWriter
    {
        private static string _directory = System.AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "PGCreator";
        private static string _fileName = @"\BuildingPGs";
        private static int _num = 0;
        private static PGWriter _PGWriter = null;
        private static string _PGInfo = "";
        private static AdditionalInfo _addiInfo;

        private PGWriter(string dir = null)
        {
            if (dir != null) _directory = dir;
        }
        public static void SetWriter(AdditionalInfo addiInfo)
        {
            if (_PGWriter == null)
            {
                _addiInfo = addiInfo;
                _PGWriter = new PGWriter(_addiInfo.outPath);
            }
        }
        public static PGWriter GetWriter()
        {
            return _PGWriter;
        }
        public void UpdatePGs(List<PGItem> pgItems)
        {
            foreach (PGItem pgItem in pgItems)
            {
                ++_num;
                _PGInfo += pgItem.PGName + "\t"
                        + pgItem.Code + "\t"
                        + ((byte)pgItem.direction).ToString() + "\t"
                        + ((pgItem.IfDefinePrice)?"1":"0") + "\t"
                        + pgItem.PinYinSuffix + "\t"
                        + string.Format("{0:f}",pgItem.Price) + "\t";
                foreach (double num in pgItem.Num)
                {
                    _PGInfo += num + "\t";
                }
                _PGInfo += "\r\n";
            }
        }
        public static void Output()
        {
            string head = "1\t建筑名称\t" + _addiInfo.bldgName + "\t行数\t" + _num.ToString()
                + "\t层数\t" + (MyLevel.GetLevelNum() - 1).ToString() + "\t\t\t\t\t\r\n"
                + "PG名称\t编码\t方向\t是否自定义价格\t拼音后缀\t单价（美元）\t";
            int i = 1;
            while (i < MyLevel.GetLevelNum())
            {
                head += i.ToString() + "\t";
                ++i;
            }
            head += "\r\n";
            _PGInfo = head + _PGInfo;
            _fileName += "_" + _addiInfo.rvtFileName + ".txt";
            FileStream fs = new FileStream(_directory + _fileName, FileMode.Create);
            StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.Default);       //write in ANSI
            sw.Write(_PGInfo);
            sw.Flush();
            sw.Close();
            fs.Close();
        }
    }

    public class AbandonmentWriter
    {
        private static string _directory = System.AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "PGCreator";
        private static string _fileName = @"\AbandonedElements";
        private static int _num = 0;
        private static AbandonmentWriter _abandonmentWriter = null;
        private static string _abandonment = null;
        private static AdditionalInfo _addiInfo = null;

        private AbandonmentWriter(string dir = null)
        {
            if (dir != null) _directory = dir;
        }
        public static void SetWriter(AdditionalInfo addiInfo)
        {
            if (_abandonmentWriter == null)
            {
                _addiInfo = addiInfo;
                _abandonmentWriter = new AbandonmentWriter(_addiInfo.outPath);
            }
        }
        public static AbandonmentWriter GetWriter()
        {
            return _abandonmentWriter;
        }
        public void WriteAbandonment(Element ele, AbandonmentTable abonTable)
        {
            _abandonment += ++_num + "\t" + ele.Category.Name.ToString() + "\t" + ele.Id.ToString() + "\t" 
                + ((int)abonTable).ToString().PadLeft(4,'0') + "\r\n";
        }
        public static void Output()
        {
            _fileName += _addiInfo.rvtFileName + ".txt";
            string head = _addiInfo.bldgName + "\t" + _addiInfo.bldgUse + "\t" + _addiInfo.builtYear + "\r\n";
            _abandonment = head + _abandonment;
            FileStream fs = new FileStream(_directory + _fileName, FileMode.Create);
            StreamWriter sw = new StreamWriter(fs);         //ele name may not be transferred correctly if use ANSI
            sw.Write(_abandonment);
            sw.Flush();
            sw.Close();
            fs.Close();
        }
    }

    public class Bitmap
    {
	    private int[] _F;
        private int[] _T;
        private int _num = 0;
        private int _top = 0;

	    private bool valid(int r) { return (0 <= r) && (r < _top); }
        private bool erased(int k){ return valid(_F[k]) && (_T[_F[k]] + 1 + k == 0); }
	    public Bitmap(int n = 8)
        {
            _F = new int[n];
            _T = new int[n];
        }
        public Bitmap Set(int k)
        {
            if (Test(k)) return this;
            if (!erased(k)) _F[k] = _top++;
            _T[_F[k]] = k;
            return this;
        }
        public bool Test(int k)
        {
            return valid(_F[k]) && (k == _T[_F[k]]);
        }
        public void Clear(int k)
        {
            if (Test(k))
            {
                _T[_F[k]] = -1 - k;
                _num--;
            }
        }
        public void Init()
        {
            _top = 0;
            _num = 0;
        }
        public int GetNum()
        {
            return _num;
        }
    }

    public class AdditionalInfo
    {
        public string outPath;
        public string rvtFileName;
        public string bldgName;
        public string bldgUse;
        public string struType;
        public string builtYear;
        public MomentFrameType mfType;
        public SDC sdc;
        public int[] unCheckedLevel = new int[MyLevel.GetLevelNum()];
        public bool[] requiredComp = new bool[10];
        public double[] prices = new double[10];
        public AdditionalInfo(char[] outFile)
        {
            /*
            outFile format:
            0th line: 0 + '\r\n'
            1st line: output path + '\r\n'
            2nd line: rvt file name + '\t' + bldg name + '\t' + bldg usage + '\t' + structural type + '\t' + built year + '\t\r\n'
            3rd line: unchecked level index(uli1) + '\t' + uli2 + '\t' + ...(num uncertain) + '\r\n'
                                    uli indicates the index of the corresponding level in MyLevel
            4th line: moment frame type + '\t' + sdc + '\t\r\n' + ...(so far 2) + '\r\n'
                      MFtype: 0-SMF, 1-comfirmedMF, 2-IMF, 3-OMF, 4-uncomfirmedMF
                      SDC: 0-A, 1-B, 2-C, 3-D, 4-E, 5-F
            5th line: checked component(cc1) + '\t' + price1 + '\t' + cc2 + '\t' + price2 + '\t' + ...(num uncertain) + '\r\n'
                      cc: 0-beam column joints, 1-shear wall
                      price: 0.00 if default
             */
            int i = 3, hot = 3;
            string temp = null;
            int tempIndex = 0;
            //1: output path
            while (outFile[i] != '\r') ++i;
            outPath = new string(outFile, hot, i - hot);
            hot = i += 2;
            //2: basic info
            while (outFile[i] != '\t') ++i;
            rvtFileName = new string(outFile, hot, i - hot);
            hot = ++i;
            while (outFile[i] != '\t') ++i;
            bldgName = new string(outFile, hot, i - hot);
            hot = ++i;
            while (outFile[i] != '\t') ++i;
            bldgUse = new string(outFile, hot, i - hot);
            hot = ++i;
            while (outFile[i] != '\t') ++i;
            struType = new string(outFile, hot, i - hot);
            hot = ++i;
            while (outFile[i] != '\t') ++i;
            builtYear = new string(outFile, hot, i - hot);
            hot = i += 3;
            //3: levels
            if (outFile[i] == '\r')
            {
                hot = i += 2;
            }
            else
            {
                while (outFile[i] != '\r')
                {
                    ++i;
                    if (outFile[i] == '\t')
                    {
                        temp = new string(outFile, hot, i - hot);
                        unCheckedLevel[(int.Parse(temp))] = 1;
                        hot = ++i;
                    }
                }
                hot = i += 2;
            }
            //4: structural info
                //MFType
            switch (outFile[i])
            {
                case '0':
                    mfType = MomentFrameType.SMF;
                    break;
                case '1':
                    mfType = MomentFrameType.confirmedMF;
                    break;
                case '2':
                    mfType = MomentFrameType.IMF;
                    break;
                case '3':
                    mfType = MomentFrameType.OMF;
                    break;
                case '4':
                    mfType = MomentFrameType.unconfirmedMF;
                    break;
                default:
                    ErrorWriter.GetWriter().WriteError("ClassLib.AdditionalInfo: MFType not found.");
                    break;
            }
                //SDC
            i += 2;
            switch (outFile[i])
            {
                case '0':
                    sdc = SDC.A;
                    break;
                case '1':
                    sdc = SDC.B;
                    break;
                case '2':
                    sdc = SDC.C;
                    break;
                case '3':
                    sdc = SDC.D;
                    break;
                case '4':
                    sdc = SDC.E;
                    break;
                case '5':
                    sdc = SDC.F;
                    break;
                default:
                    ErrorWriter.GetWriter().WriteError("ClassLib.AdditionalInfo: SDC not found.");
                    break;
            }
                //add here
            hot = i += 4;
            //5: component
            if (outFile[i] != '\r')
            {
                while (outFile[i] != '\r')
                {
                    ++i;
                    if (outFile[i] == '\t')
                    {
                        temp = new string(outFile, hot, i - hot);
                        tempIndex = int.Parse(temp);
                        requiredComp[tempIndex] = true;
                        hot = ++i;
                        while (outFile[i] != '\t') ++i;
                        temp = new string(outFile, hot, i - hot);
                        prices[tempIndex] = double.Parse(temp);
                        hot = ++i;
                    }                       
                }
            }
        }
    }
}