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
    private Text m_Text;
    private float timer = 0f;
    private float time = 0f;
    private int row_index = 0;

    private float XArea = 0f;
    private float YArea = 0f;
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
        ExcelFile = new FileInfo(excel_path);
        package = new ExcelPackage(ExcelFile);
        worksheet = package.Workbook.Worksheets.Add(DateTime.Now.ToString("MM-dd HH:mm:ss"));

        m_Text = TextUI.GetComponent<Text>();
    }
    void Start()
    {
        Load();
    }

    // Update is called once per frame
    void Update()
    {

        XArea = ResourceLocatorService.Instance.XArea;
        YArea = ResourceLocatorService.Instance.YArea;
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
