﻿using Alturos.PanTilt.Calibration.Extension;
using Alturos.PanTilt.Calibration.Model;
using Alturos.PanTilt.Communication;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Alturos.PanTilt.Calibration
{
    public partial class Main : Form
    {
        private List<SpeedReport> _speedReports = new List<SpeedReport>();
        private BindingSource _bindingSource = new BindingSource();
        private ICommunication _communication;
        private PanTiltControlType _panTiltControlType;
        private CancellationTokenSource _cancellationTokenSource;

        public Main()
        {
            this.InitializeComponent();
            this._bindingSource.DataSource = this._speedReports;
            this.dataGridViewAxis.DataSource = this._bindingSource;

            this.comboBoxAxisType.DataSource = Enum.GetValues(typeof(AxisType));
            this.comboBoxAxisType.SelectedIndex = 0;
            this.buttonAbortQuickCheck.Enabled = false;

            var dialog = new CommunicationDialog
            {
                StartPosition = FormStartPosition.CenterScreen
            };
            var dialogResult = dialog.ShowDialog(this);
            if (dialogResult == DialogResult.OK)
            {
                this._communication = dialog.Communication;
                this._panTiltControlType = dialog.PanTiltControlType;
            }
        }

        private async void buttonSpeedLogicPan_Click(object sender, EventArgs e)
        {
            this._speedReports.Clear();
            await Task.Run(() => StartPanTest());
        }

        private async void buttonSpeedLogicTilt_Click(object sender, EventArgs e)
        {
            this._speedReports.Clear();
            await Task.Run(() => StartTiltTest());
        }

        private void StartPanTest()
        {
            using (var logic = new SpeedDetectionLogicPan(this._communication, this._panTiltControlType))
            {
                var sw = new Stopwatch();
                var startPosition = -100;
                var endPosition = startPosition + 2;
                var timeout = 4000;
                for (var i = 1; i < 1000; i++)
                {
                    var speed = i / 10.0;

                    //if (i == 200)
                    //{
                    //    endPosition = 0;
                    //    timeout = 2000;
                    //}

                    try
                    {
                        while (true)
                        {
                            logic.GoToStartPosition(startPosition);
                            sw.Restart(); //go to 0 degree
                            logic.Start(speed, endPosition);
                            sw.Stop();

                            if (sw.Elapsed.TotalMilliseconds < timeout)
                            {
                                endPosition += 2;
                                continue;
                            }

                            var item = new SpeedReport { Speed = speed, Distance = endPosition + -startPosition, Elapsed = sw.Elapsed.TotalMilliseconds };
                            this._speedReports.Add(item);

                            this.dataGridViewAxis.Invoke((MethodInvoker)delegate { this._bindingSource.ResetBindings(false); });

                            break;
                        }
                    }
                    catch (Exception exception)
                    {
                        Trace.Write(exception.ToString());
                    }
                }
            }
        }

        private void StartTiltTest()
        {
            using (var logic = new SpeedDetectionLogicTilt(this._communication, this._panTiltControlType))
            {
                var sw = new Stopwatch();
                var startPosition = -15;
                var endPosition = startPosition + 2;
                var timeout = 2000;
                for (var speed = 1; speed < 255; speed++)
                {
                    if (speed == 200)
                    {
                        endPosition = 0;
                        timeout = 1000;
                    }

                    try
                    {
                        while (true)
                        {
                            logic.GoToStartPosition(startPosition);
                            sw.Restart(); //go to 0 degree
                            logic.Start(speed, endPosition);
                            sw.Stop();

                            if (sw.Elapsed.TotalMilliseconds < timeout)
                            {
                                endPosition += 2;
                                continue;
                            }

                            var item = new SpeedReport { Speed = speed, Distance = endPosition + -startPosition, Elapsed = sw.Elapsed.TotalMilliseconds };
                            this._speedReports.Add(item);

                            this.dataGridViewAxis.Invoke((MethodInvoker)delegate { this._bindingSource.ResetBindings(false); });

                            break;
                        }
                    }
                    catch (Exception exception)
                    {
                        Trace.Write(exception.ToString());
                    }
                }
            }
        }

        private async void buttonCheck_Click(object sender, EventArgs e)
        {
            this.buttonCheck.Enabled = false;
            this.buttonAbortQuickCheck.Enabled = true;

            this._cancellationTokenSource?.Dispose();
            this._cancellationTokenSource = new CancellationTokenSource();

            double.TryParse(this.textBoxDegreePerSecond.Text, out var degreePerSecond);
            int.TryParse(this.textBoxDriveMilliseconds.Text, out var driveMilliseconds);
            int.TryParse(this.textBoxStartPosition.Text, out var startPosition);

            var items = new List<PositionCompare>();
            var bindingSource = new BindingSource
            {
                DataSource = items
            };
            this.dataGridViewQuickCheck.DataSource = bindingSource;

            var axisType = (AxisType)this.comboBoxAxisType.SelectedItem;

            await Task.Run(async () =>
            {
                using (var logic = new SpeedTestLogic(this._communication, this._panTiltControlType, axisType))
                {
                    logic.GoToStartPosition(startPosition);

                    var breakpoints = new List<double>();
                    for (var j = 0; j < 80; j++)
                    {
                        if (this._cancellationTokenSource.IsCancellationRequested)
                        {
                            return;
                        }

                        var destinationPosition = startPosition + (degreePerSecond * driveMilliseconds / 1000);

                        for (var i = 0; i < 3; i++)
                        {
                            logic.GoToStartPosition(startPosition);
                            logic.Move(degreePerSecond, driveMilliseconds);

                            await Task.Delay(300, this._cancellationTokenSource.Token);

                            var lastPosition = logic.LastPosition;
                            breakpoints.Add(lastPosition);

                            if (this._cancellationTokenSource.IsCancellationRequested)
                            {
                                return;
                            }
                        }

                        var item = new PositionCompare
                        {
                            DegreePerSecond = degreePerSecond,
                            MoveTime = driveMilliseconds,
                            ActualPosition = Math.Round(breakpoints.Average(), 2),
                            TargetPosition = destinationPosition
                        };

                        items.Add(item);

                        this.dataGridViewQuickCheck.Invoke(o => ((BindingSource)o.DataSource).ResetBindings(true));

                        degreePerSecond += 0.5;
                        breakpoints.Clear();
                    }
                }
            }, this._cancellationTokenSource.Token).ContinueWith(t => { });

            this.buttonCheck.Enabled = true;
            this.buttonAbortQuickCheck.Enabled = false;
        }

        private void dataGridView2_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            var datagrid = sender as DataGridView;

            var item = datagrid.Rows[e.RowIndex].DataBoundItem as PositionCompare;
            var tolerance = item.DegreePerSecond / 100 * 5;

            if (item.DifferencePerSecond > tolerance)
            {
                datagrid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.Beige;
            }
            else
            {
                datagrid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.GreenYellow;
            }
        }

        private void buttonAbortQuickCheck_Click(object sender, EventArgs e)
        {
            this._cancellationTokenSource?.Cancel();
        }
    }
}
