using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using System.Text;
using OfficeOpenXml;
using UnityEngine.UI;
using OneBitLab.Services;

public class BoatDebugging : MonoBehaviour
{
    public GameObject TextUI;

    private string path;
    private string excel_path;
    FileInfo ExcelFile;
    ExcelPackage package;
    ExcelWorksheet worksheet;
    ExcelWorksheet worksheet2;
    ExcelWorksheet worksheet3;
    ExcelWorksheet worksheet4;
    ExcelWorksheet worksheet5;
    ExcelWorksheet worksheet6;
    private Text m_Text;

    private float timer = 0f;
    private float time = 0f;
    private int row_index = 0;

    private float XArea = 0f;
    private float YArea = 0f;
    private float FX = 0f;
    private float FY = 0f;
    private float FXOld = 0f;
    private float FYOld = 0f;
    private float posx = 0f;
    private float posz = 0f;

    private Vector3 ZDir = new Vector3(0, 0, 0);
    private Vector3 up = new Vector3(0, 1.0f, 0);
    private Vector3 StandardZDir = new Vector3(0, 0.99354f, -0.011345f);
    private float zangle =0f;
    private float zangle2 = 0f;

    private Vector3 XDir = new Vector3(0, 0, 0);
    private Vector3 right = new Vector3(1.0f, 0, 0);
    private float xangle = 0f;
   
    // Start is called before the first frame update
    void Load()
    {

        /*string path = AppDomain.CurrentDomain.BaseDirectory;*/
        path = "D:\\StudyAndWork\\�ж�\\�Ϻ�\\ˮ��ģ��\\������\\WaveParticles\\Assets\\Scripts\\Log\\Boat";//D:\StudyAndWork\�ж�\�Ϻ�\ˮ��ģ��\������\WaveParticles\Assets\Scripts\Log\Boat
        //path = "D:\\StudyAndWork\\�ж�\\Log";
        //����ϴ�������·���Ƿ���ڣ��������򴴽�
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        //excel
        excel_path = path + "\\" + DateTime.Now.ToString("MM-dd HH mm") + "data.xlsx";
        //excel_path = path + "\\" + DateTime.Now.ToString("MM-dd HH mm") + "Force.xlsx";

        ExcelFile = new FileInfo(excel_path);
        package = new ExcelPackage(ExcelFile);
        worksheet = package.Workbook.Worksheets.Add(DateTime.Now.ToString("MM-dd HH:mm:ss"));
        worksheet2 = package.Workbook.Worksheets.Add("force");
        worksheet3 = package.Workbook.Worksheets.Add("oldforce");
        worksheet4 = package.Workbook.Worksheets.Add("position");
        worksheet5 = package.Workbook.Worksheets.Add("��ҡ");//��ҡ
        worksheet6 = package.Workbook.Worksheets.Add("��ҡ");

        m_Text = TextUI.GetComponent<Text>();

    }
    void Start()
    {
        Load();
    }

    // Update is called once per frame
    void Update()
    {

        updateArea();


    }
    
    private void updateForce()
    {
        FX = ResourceLocatorService.Instance.FX;
        FY = ResourceLocatorService.Instance.FY;
        //��ʱ��log����д��
        //Debug.Log("label"+label);
        timer += Time.deltaTime;
        if (timer >= 0.1)// ��ʱ0.1�� 10hz
        {
            //writeLog();
            time += timer;
            timer = 0f;

            //д��excel
            row_index++;
            worksheet.Cells[row_index, 1].Value = time;
            worksheet.Cells[row_index, 2].Value = FX;
            worksheet.Cells[row_index, 3].Value = FY;

            m_Text.text = time.ToString();
        }
    }
    private void updateArea()
    {
        XArea = ResourceLocatorService.Instance.XArea;
        YArea = ResourceLocatorService.Instance.YArea;
        FX = ResourceLocatorService.Instance.FX;
        FY = ResourceLocatorService.Instance.FY;
        FXOld = ResourceLocatorService.Instance.FXOld;
        FYOld = ResourceLocatorService.Instance.FYOld;
        posx = ResourceLocatorService.Instance.WorldCOM.x;
        posz = ResourceLocatorService.Instance.WorldCOM.z;

        ZDir = ResourceLocatorService.Instance.ZDir;
        //����ǶȲ�
        zangle = Vector3.SignedAngle(ZDir, Vector3.up, Vector3.forward);
        zangle2 = Vector3.Angle(ZDir, StandardZDir);

        XDir = ResourceLocatorService.Instance.XDir;
        //����ǶȲ�
        xangle = Vector3.SignedAngle(XDir, Vector3.forward, Vector3.up);
        //��ʱ��log����д��
        //Debug.Log("label"+label);
        timer += Time.deltaTime;
        if (timer >= 0.1)// ��ʱ0.1�� 10hz
        {
            //writeLog();
            time += timer;
            timer = 0f;

            //д��excel
            row_index++;
            worksheet.Cells[row_index, 1].Value = time;
            worksheet.Cells[row_index, 2].Value = XArea;
            worksheet.Cells[row_index, 3].Value = YArea;

            worksheet2.Cells[row_index, 1].Value = time;
            worksheet2.Cells[row_index, 2].Value = FX;
            worksheet2.Cells[row_index, 3].Value = FY;

            worksheet3.Cells[row_index, 1].Value = time;
            worksheet3.Cells[row_index, 2].Value = FXOld;
            worksheet3.Cells[row_index, 3].Value = FYOld;

            worksheet4.Cells[row_index, 1].Value = time;
            worksheet4.Cells[row_index, 2].Value = posx;
            worksheet4.Cells[row_index, 3].Value = posz;

            worksheet5.Cells[row_index, 1].Value = time;
            worksheet5.Cells[row_index, 2].Value = zangle;
            worksheet5.Cells[row_index, 3].Value = zangle2;
            worksheet5.Cells[row_index, 4].Value = ZDir;

            worksheet6.Cells[row_index, 1].Value = time;
            worksheet6.Cells[row_index, 2].Value = xangle;

            m_Text.text = time.ToString();
        }
    }
    private void OnDestroy()
    {
        //�ر�Ӧ�ó���
        package.Save();
        Debug.Log("����Excel�ɹ�");
    }
}
