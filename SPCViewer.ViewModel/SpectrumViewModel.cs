﻿using ChemSharp.Spectroscopy;
using ChemSharp.Spectroscopy.DataProviders;
using ChemSharp.Spectroscopy.Extension;
using OxyPlot;
using OxyPlot.Series;
using SPCViewer.Core;
using SPCViewer.Core.Extension;
using SPCViewer.Core.Plots;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SPCViewer.ViewModel
{
    public class SpectrumViewModel : INotifyPropertyChanged
    {
        private Spectrum _spectrum;
        /// <summary>
        /// Contains the shown spectrum
        /// </summary>
        public Spectrum Spectrum
        {
            get => _spectrum;
            set
            {
                _spectrum = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Pass Through the Spectrums title
        /// </summary>
        public string Title => Path.GetFileName(Spectrum?.Title);

        /// <summary>
        /// The used PlotModel
        /// </summary>
        public DefaultPlotModel Model { get; }

        /// <summary>
        /// The Series containing experimental data
        /// </summary>
        public LineSeries ExperimentalSeries { get; set; }

        /// <summary>
        /// The Series containing integrated data
        /// </summary>
        public LineSeries IntegralSeries { get; set; }

        /// <summary>
        /// The Series containing derived data
        /// </summary>
        public LineSeries DerivSeries { get; set; }

        /// <summary>
        /// List of Integrals
        /// </summary>
        public ObservableCollection<Integral> Integrals { get; set; } = new ObservableCollection<Integral>(new List<Integral>());

        /// <summary>
        /// Currently Active UI Action
        /// </summary>
        private UIAction _mouseAction;
        public UIAction MouseAction
        {
            get => _mouseAction;
            set
            {
                _mouseAction = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="path"></param>
        public SpectrumViewModel(string path)
        {
            var provider = ExtensionHandler.Handle(path);
            Spectrum = new Spectrum() { DataProvider = provider };
            Model = new DefaultPlotModel();
            InitSeries();
            InitModel();
            Model.Series.Add(ExperimentalSeries);
            Model.Series.Add(IntegralSeries);
            Model.Series.Add(DerivSeries);
            PropertyChanged += OnPropertyChanged;
        }

        private void InitModel()
        {
            Controller = PlotControls.DefaultController;
            Model.Title = Path.GetFileName(Spectrum.Title);
            if (Spectrum.DataProvider is BrukerNMRProvider) Model.InvertX();
            if (Spectrum.DataProvider is BrukerEPRProvider || Spectrum.DataProvider is BrukerNMRProvider) Model.ToggleY();
            //setup x axis 
            Model.XAxis.Title = Spectrum.Quantity();
            Model.XAxis.Unit = Spectrum.Unit();
            Model.XAxis.AbsoluteMinimum = Spectrum.XYData.Min(s => s.X);
            Model.XAxis.AbsoluteMaximum = Spectrum.XYData.Max(s => s.X);
            //setup y axis
            Model.YAxis.Title = Spectrum.YQuantity();
        }

        /// <summary>
        /// Initializes Series Data Binding
        /// </summary>
        private void InitSeries()
        {
            ExperimentalSeries = new LineSeries
            {
                ItemsSource = Spectrum.XYData,
                Mapping = s => ((ChemSharp.DataPoint)s).Mapping()
            };
            IntegralSeries = new LineSeries
            {
                ItemsSource = Spectrum.Integral,
                Mapping = s => ((ChemSharp.DataPoint)s).Mapping(),
                IsVisible = false
            };
            DerivSeries = new LineSeries()
            {
                ItemsSource = Spectrum.Derivative,
                Mapping = s => ((ChemSharp.DataPoint)s).Mapping(),
                IsVisible = false
            };
        }

        /// <summary>
        /// Gets the PlotController
        /// </summary>
        public PlotController Controller { get; private set; }

        /// <summary>
        /// Adds an Integral to List
        /// </summary>
        /// <param name="rect"></param>
        private void AddIntegral((DataPoint, DataPoint) rect)
        {
            var (item1, item2) = rect;
            var min = item1.X < item2.X ? item1 : item2;
            var max = item1.X > item2.X ? item1 : item2;
            var points = Spectrum.XYData.Where(s => s.X >= min.X && s.X <= max.X).ToArray();
            if(!points.Any()) return;
            Integrals.Add(new Integral(points));
        }

        /// <summary>
        /// <inheritdoc cref="INotifyPropertyChanged.PropertyChanged" />
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(MouseAction)) return;
            //bind action to plotcontroller
            Action<(DataPoint, DataPoint)> rectAction = MouseAction switch
            {
                UIAction.Integrate => AddIntegral,
                _ => null
            };
            var action = new DelegatePlotCommand<OxyMouseDownEventArgs>((view, controller, args) =>
                controller.AddMouseManipulator(view, new Core.Plots.ZoomRectangleManipulator(view, rectAction), args));
            Controller.BindMouseDown(OxyMouseButton.Left, action);
        }

        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// <inheritdoc cref="INotifyPropertyChanged.PropertyChanged" />
        /// </summary>
        /// <param name="propertyName"></param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}