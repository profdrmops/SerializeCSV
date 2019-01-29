using Dynamitey;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using YamlDotNet.Serialization;

namespace SerializeCSV
{
    internal enum TargetMode
    {
        JSON,
        XML,
        YML
    }
    
    internal class Program
    {
        private TargetMode targetMode;
        private readonly string input = string.Empty;
        private List<dynamic> output = new List<dynamic>();
        private char separator;
        private int numberOfColumns;
        private List<string> columnNames = new List<string>();
        private List<Type> columnTypes = new List<Type>();
        private string finalString;
        
        private static void Main(string[] args)
        {
            new Program().Convert(args);
        }
        
        private void Convert(string[] args)
        {
            try
            {
                switch (args[0])
                {
                    case "json":
                    case "JSON": targetMode = TargetMode.JSON; break;
                    case "xml":
                    case "XML": targetMode = TargetMode.XML; break;
                    case "yml":
                    case "yaml":
                    case "Yaml":
                    case "YML": targetMode = TargetMode.YML; break;
                    default: throw new ArgumentException($"Invalid target file format: {args[0]}.");
                }
                Console.WriteLine($"[INFO]\tSetting target mode to {args[0]}...");
                
                if (string.IsNullOrEmpty(args[1]))
                {
                    throw new ArgumentException("No input file specified.");
                }
                
                separator = args[2].Length == 1 ? args[2][0] : throw new ArgumentException($"Invalid CSV separator char: {args[2]}");
                
                Console.WriteLine($"[INFO]\tOpening file {args[1]}...");
                
                using (FileStream fileStream = new FileStream(args[1], FileMode.Open))
                {
                    using (StreamReader streamReader = new StreamReader(fileStream))
                    {
                        string s = string.Empty;
                        try
                        {
                            s = streamReader.ReadLine();
                            numberOfColumns = s.Contains(separator) ? s.Split(separator).Length : throw new ArgumentException($"{args[1]} does not conatin separator char {args[2]}");
                            fileStream.Position = 0;
                            streamReader.DiscardBufferedData();
                        }
                        catch (FormatException ex)
                        {
                            throw new ArgumentException($"Invalid CSV separator char: {args[2]}", ex);
                        }
                        
                        if (string.IsNullOrEmpty(s))
                            throw new FileLoadException($"Could not read file {args[1]}.");
                        
                        if (args.Length >= 5 && (args[4] == "-q" || args[4] == "--quick"))
                        {
                            Console.WriteLine($"[INFO]\tQuick argument passed.\n[WARN]\tParsing every column as string and naming alphabetically.");
                            char c = 'A';
                            foreach (int i in Enumerable.Range(0, numberOfColumns))
                            {
                                columnNames.Add(c.ToString());
                                columnTypes.Add(typeof(string));
                                ++c;
                            }
                        }
                        else if(args.Length >= 5 && (args[4] == "-a" || args[4] == "--auto"))
                        {
                            Console.WriteLine($"[INFO]\tAuto argument passed.\n[WARN]\tTrying to guess column types. Naming alphabetically.");
                            char c = 'A';
                            foreach (int i in Enumerable.Range(0, numberOfColumns))
                            {
                                columnNames.Add(c.ToString());
                                columnTypes.Add(Guess(s.Split(separator)[i]));
                                ++c;
                            }
                        }
                        else
                        {
                            foreach (int i in Enumerable.Range(0, numberOfColumns))
                            {
                                Console.Write($"[COLUMN {i + 1}] (e.g. \"{s.Split(separator)[i]}\")\n[NAME]\t");
                                columnNames.Add(Console.ReadLine());
                                Console.Write($"[TYPE]\t(Guess: {Guess(s.Split(separator)[i]).Name}) ");
                                string type = Console.ReadLine();
                                switch (type)
                                {
                                    case "String":
                                    case "s":
                                    case "string": columnTypes.Add(typeof(string)); break;
                                    case "Int":
                                    case "integer":
                                    case "i":
                                    case "Integer":
                                    case "int": columnTypes.Add(typeof(int)); break;
                                    case "Bool":
                                    case "boolean":
                                    case "Boolean":
                                    case "b":
                                    case "bool": columnTypes.Add(typeof(bool)); break;
                                    case "double":
                                    case "Double":
                                    case "f":
                                    case "d":
                                    case "Float":
                                    case "float": columnTypes.Add(typeof(double)); break;
                                    case "": columnTypes.Add(Guess(s.Split(separator)[i])); break;
                                    default: throw new ArgumentException($"Type {type} not supported. Valid types: string, integer, float/double, boolean.");
                                }
                            }
                        }
                        
                        dynamic temp = null;
                        Console.WriteLine("[INFO]\tParsing data...");
                        for (string currentLine = streamReader.ReadLine(); currentLine != null; currentLine = streamReader.ReadLine())
                        {
                            temp = new System.Dynamic.ExpandoObject();
                            foreach (int i in Enumerable.Range(0, numberOfColumns))
                            {
                                Dynamic.InvokeSet(temp, columnNames[i], Cast[columnTypes[i]](currentLine.Split(separator)[i]));
                            }
                            output.Add(temp);
                        }
                    }
                }
                
                Console.WriteLine("[INFO]\tReserializing...");
                switch (targetMode)
                {
                    case TargetMode.JSON: finalString = JsonConvert.SerializeObject(output, Newtonsoft.Json.Formatting.Indented); break;
                    case TargetMode.XML:
                        {
                            finalString = XDocument.Load(
                                JsonReaderWriterFactory.CreateJsonReader(
                                    Encoding.ASCII.GetBytes(
                                        JsonConvert.SerializeObject(
                                            output)), new XmlDictionaryReaderQuotas()))
                                .ToString(); //REEEEEEEEEEEEEEEEEEEEEEEE
                            break;
                        }
                    case TargetMode.YML: finalString = new SerializerBuilder().Build().Serialize(output); break;
                }
                
                string outputPath = string.Empty;
                if (args.Length <= 3)
                {
                    outputPath = $"{Path.GetFileNameWithoutExtension(args[1])}.{targetMode.ToString()}";
                }
                else
                {
                    outputPath = args[3];
                }
                
                Console.WriteLine($"[INFO]\tWriting output file {outputPath}...");
                using (StreamWriter streamWriter = File.CreateText(outputPath))
                {
                    streamWriter.Write(finalString);
                }
                
                Console.WriteLine("[INFO]\tDone.");
            }
            catch (FileNotFoundException fileNotFound)
            {
                Console.WriteLine($"[ERROR]\tFile {args[1]} not found.", fileNotFound);
            }
            catch(Exception e)
            {
                Console.WriteLine($"[ERROR]\t{e.Message}\n[INFO]\tExiting...");
            }
        }
        
        private readonly Dictionary<Type, Func<string, object>> Cast = new Dictionary<Type, Func<string, object>>
        {
            { typeof(int),      (input) => int.Parse(input) },
            { typeof(double),   (input) => double.Parse(input.Replace('.', ',')) },
            { typeof(string),   (input) => input },
            { typeof(bool),     (input) => bool.Parse(input) }
        };
        
        private Type Guess(string str)
        {
            var supportedTypes = new  List<Type> { typeof(int), typeof(double), typeof(bool), typeof(string) };
            
            foreach (Type t in supportedTypes)
            {
                TypeConverter converter = TypeDescriptor.GetConverter(t);
                if(converter.CanConvertFrom(typeof(string)))
                {
                    try
                    {
                        object temp = converter.ConvertFromInvariantString(str);
                        if (temp != null)
                            return t;
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            
            return null;
        }
    }
}
