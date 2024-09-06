using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using System.Text;
using OfficeOpenXml;
using UnityEngine.UI;

public class ShaderDebugging : MonoBehaviour
{
    public GameObject TextUI;

    private Material material;
    private ComputeBuffer buffer;
    private Vector3[] element;
    private string label;
    private MeshRenderer render;
    private float timer = 0f;
    private float time = 0f;
    private Text m_Text;
    private int row_index = 0;
    StreamWriter stream;
    //����excelӦ�ó���
    FileInfo ExcelFile;
    ExcelPackage package;
    ExcelWorksheet worksheet;
    private string path;
    private string excel_path;

    void Load()
    {
        buffer = new ComputeBuffer(1, 12, ComputeBufferType.Default);//strideҲҪ�Ĵ�С...
        element = new Vector3[1];
        label = string.Empty;
        render = GetComponent<MeshRenderer>();
        material = render.material;

        /*string path = AppDomain.CurrentDomain.BaseDirectory;*/
        path = "D:\\StudyAndWork\\�ж�\\�Ϻ�\\ˮ��ģ��\\������\\WaveParticles\\Assets\\Scripts\\Log";
        //path = "D:\\StudyAndWork\\�ж�\\Log";
        //����ϴ�������·���Ƿ���ڣ��������򴴽�
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        stream = new StreamWriter(path+ "\\" + DateTime.Now.ToString("MM-dd HH mm") + "log.txt", true, Encoding.Default);

        //excel
        excel_path = path + "\\" + DateTime.Now.ToString("MM-dd HH mm") + "data.xlsx";
        ExcelFile = new FileInfo(excel_path);
        package = new ExcelPackage(ExcelFile);
        worksheet = package.Workbook.Worksheets.Add(DateTime.Now.ToString("MM-dd HH:mm:ss"));

        m_Text = TextUI.GetComponent<Text>();
    }
    // Start is called before the first frame update
    void Start()
    {
        Load();
    }

    // Update is called once per frame
    void Update()
    {
        /*
        Graphics.ClearRandomWriteTargets();
        material.SetPass(0);
        material.SetBuffer("buffer", buffer);
        Graphics.SetRandomWriteTarget(1, buffer, false);
        buffer.GetData(element);
        label = (element != null & render.isVisible) ? element[0].ToString("F3") : string.Empty;
        //��ʱ��log����д��
        //Debug.Log("label"+label);
        timer += Time.deltaTime;
        if (timer >= 0.1)// ��ʱ0.1�� 10hz
        {
            //writeLog();
            stream.Write(DateTime.Now.ToString() + ":" + label);
            stream.Write("\r\n");
            stream.Flush();
            //time += 0.1f;
            time += timer;
            timer = 0f;

            //д��excel
            row_index++;
            worksheet.Cells[row_index, 1].Value = time;
            worksheet.Cells[row_index, 2].Value = element[0].y;
            worksheet.Cells[row_index, 3].Value = label;

            m_Text.text = time.ToString();
        }
        */
    }
    private void writeLog()
    {
        StreamWriter stream;
        //д����־����
        string path = AppDomain.CurrentDomain.BaseDirectory;
        //����ϴ�������·���Ƿ���ڣ��������򴴽�
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        stream = new StreamWriter(path + "\\log.txt", true, Encoding.Default);
        stream.Write(DateTime.Now.ToString() + ":" + label);
        stream.Write("\r\n");
        stream.Flush();
        stream.Close();
    }
/*    private void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 32;
        GUI.Label(new Rect(50, 50, 400, 100), label, style);
    }*/
    private void OnDestroy()
    {
        buffer.Dispose();
        stream.Close();
        //�ر�Ӧ�ó���
        package.Save();
        Debug.Log("����Excel�ɹ�");
        //�ر����ݱ�
/*        wb.Close();
        myApp.Quit();
        System.Runtime.InteropServices.Marshal.ReleaseComObject(myApp);
        myApp = null;*/
    }
}
