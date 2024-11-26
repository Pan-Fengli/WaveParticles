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
        path = "D:\\StudyAndWork\\研二\\南湖\\水体模拟\\看代码\\WaveParticles\\Assets\\Scripts\\Log\\Boat";//D:\StudyAndWork\研二\南湖\水体模拟\看代码\WaveParticles\Assets\Scripts\Log\Boat
        //path = "D:\\StudyAndWork\\研二\\Log";
        //检查上传的物理路径是否存在，不存在则创建
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
        //定时向log里面写入
        //Debug.Log("label"+label);
        timer += Time.deltaTime;
        if (timer >= 0.1)// 定时0.1秒 10hz
        {
            //writeLog();
            time += timer;
            timer = 0f;

            //写入excel
            row_index++;
            worksheet.Cells[row_index, 1].Value = time;
            worksheet.Cells[row_index, 2].Value = XArea;
            worksheet.Cells[row_index, 3].Value = YArea;

            m_Text.text = time.ToString();
        }
        
    }
    private void OnDestroy()
    {
        //关闭应用程序
        package.Save();
        Debug.Log("导出Excel成功");
    }
}
