﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using DGCValidator.Models;
using DGCValidator.Resources;
using DGCValidator.Services;
using DGCValidator.Services.DGC;
using DGCValidator.Services.DGC.V1;
using DGCValidator.Views;
using Xamarin.Forms;
using ZXing;

namespace DGCValidator.ViewModels
{
    public class ScanViewModel : BaseViewModel
    {
        String _resultText;
        bool _resultOk = false;
        SignatureModel _signature;
        SubjectModel _subject;
        bool _hasVaccination = false;
        bool _hasTest = false;
        bool _hasRecovered = false;
        ObservableCollection<object> _certs;

        private ICommand scanCommand;
        private ICommand settingsCommand;
        private ICommand aboutCommand;

        public ScanViewModel()
        {
            _subject = new SubjectModel();
            _certs = new ObservableCollection<object>();
        }

        public Result Result { get; set; }

        public ICommand ScanCommand => scanCommand ??
        (scanCommand = new Command(async () =>
        {
            ResultViewModel resultModel = new ResultViewModel();
            resultModel.UpdateFields(Result.Text);
            ResultPage resultPage = new ResultPage();
            resultPage.BindingContext = resultModel;
            await Application.Current.MainPage.Navigation.PushAsync(resultPage);
        }));

        private void UpdateFields(String scanResult)
        {
            try
            {
                if (scanResult != null)
                {
                    var result = VerificationService.VerifyData(scanResult);
                    if (result != null)
                    {
                        SignedDGC proof = result;
                        if (proof != null)
                        {
                            Subject = new Models.SubjectModel
                            {
                                Firstname = proof.Dgc.Nam.Fn,
                                Familyname = proof.Dgc.Nam.Gn,
                                TranName = proof.Dgc.Nam.Fnt+"<<"+proof.Dgc.Nam.Gnt,
                                DateOfBirth = proof.Dgc.Dob,
                            };
                            Signature = new Models.SignatureModel
                            {
                                IssuedDate = proof.IssuedDate,
                                ExpirationDate = proof.ExpirationDate,
                                IssuerCountry = proof.IssuingCountry
                            };

                            bool fullyVaccinated = false;

                            if (proof.Dgc.V != null && proof.Dgc.V.Length > 0)
                            {
                                HasVaccinations = true;
                                foreach (VElement vac in proof.Dgc.V)
                                {
                                    if( vac.Dn >= vac.Sd)
                                    {
                                        fullyVaccinated = true;
                                    }
                                     AddCertificate(new Models.VaccineCertModel
                                    {
                                        Type = Models.CertType.VACCINE,
                                        Tg = CodeMapperUtil.GetDiseaseAgentTargeted(vac.Tg),
                                        Vp = CodeMapperUtil.GetVaccineOrProphylaxis(vac.Vp),
                                        Mp = CodeMapperUtil.GetVaccineMedicalProduct(vac.Mp),
                                        Ma = CodeMapperUtil.GetVaccineMarketingAuthHolder(vac.Ma),
                                        Dn = vac.Dn,
                                        Sd = vac.Sd,
                                        Dt = vac.Dt,
                                        Co = vac.Co,
                                        Is = vac.Is,
                                        Ci = vac.Ci
                                    });
                                }

                            }
                            if (proof.Dgc.T != null && proof.Dgc.T.Length > 0)
                            {
                                HasTest = true;
                                foreach (TElement tst in proof.Dgc.T)
                                {
                                    AddCertificate(new Models.TestCertModel
                                    {
                                        Type = Models.CertType.TEST,
                                        Tg = CodeMapperUtil.GetDiseaseAgentTargeted(tst.Tg),
                                        Tt = CodeMapperUtil.GetTestType(tst.Tt),
                                        Ma = CodeMapperUtil.GetTestMarketingAuthHolder(tst.Ma),
                                        Nm = tst.Nm,
                                        Sc = tst.Sc, /*Models.TestCertModel.ConvertFromSecondsEpoc(tst.Sc),*/
                                        //Dr = tst.Dr, /*Models.TestCertModel.ConvertFromSecondsEpoc(tst.Dr),*/
                                        Tr = CodeMapperUtil.GetTestResult(tst.Tr),
                                        Tc = tst.Tc,
                                        Co = tst.Co,
                                        Is = tst.Is,
                                        Ci = tst.Ci
                                    });
                                }
                            }
                            if (proof.Dgc.R != null && proof.Dgc.R.Length > 0)
                            {
                                HasRecovered = true;
                                foreach (RElement rec in proof.Dgc.R)
                                {
                                    AddCertificate(new Models.RecoveredCertModel
                                    {
                                        Type = Models.CertType.RECOVERED,
                                        Tg = CodeMapperUtil.GetDiseaseAgentTargeted(rec.Tg),
                                        Fr = rec.Fr,
                                        Co = rec.Co,
                                        Is = rec.Is,
                                        Df = rec.Df,
                                        Du = rec.Du,
                                        Ci = rec.Ci
                                    });
                                }
                            }

                            List<string> texts = new List<string>();
                            if(_hasVaccination)
                            {
                                if( fullyVaccinated)
                                {
                                    texts.Add(AppResources.VaccinatedText);
                                    IsResultOK = true;
                                }
                                else
                                {
                                    texts.Add(AppResources.NotFullyVaccinatedText);
                                    IsResultOK = false;
                                }
                            }
                            if(_hasTest)
                            {
                                texts.Add(AppResources.TestedText);
                                IsResultOK = false;
                            }
                            if (_hasRecovered)
                            {
                                texts.Add(AppResources.RecoveredText);
                                IsResultOK = false;
                            }
                            if( !_hasVaccination && !_hasTest && !_hasRecovered)
                            {
                                texts.Add(AppResources.MissingDataText);
                                IsResultOK = false;
                            }
                            if( proof.Message != null)
                            {
                                texts.Add(" " + proof.Message);
                                IsResultOK = false;
                            }

                            ResultText = string.Join(", ", texts.ToArray());
                        }
                        else
                        {
                            ResultText = AppResources.ErrorReadingText;
                            IsResultOK = false;

                        }
                    }
                    else
                    {
                        ResultText = AppResources.ErrorReadingText + ", " + scanResult;
                        IsResultOK = false;

                    }
                }
            }
            catch (Exception ex)
            {
                //ResultText = AppResources.ErrorReadingText + ", " + ex.Message;
                ResultText = ex.Message;
                IsResultOK = false;
            }
        }

        public bool IsVisible
        {
            get { return (_subject!=null&&_subject.Firstname!=null&&_subject.Firstname.Length>0?true:false); }
        }

        public bool HasVaccinations
        {
            get { return _hasVaccination; }
            set
            {
                _hasVaccination = value;
                OnPropertyChanged();
            }
        }

        public bool HasTest
        {
            get { return _hasTest; }
            set
            {
                _hasTest = value;
                OnPropertyChanged();
            }
        }

        public bool HasRecovered
        {
            get { return _hasRecovered; }
            set
            {
                _hasRecovered = value;
                OnPropertyChanged();
            }
        }

        public String ResultText
        {
            get { return _resultText; }
            set
            {
                _resultText = value;
                OnPropertyChanged();
            }
        }

        public bool IsResultOK
        {
            get { return _resultOk; }
            set
            {
                _resultOk = value;
                OnPropertyChanged();
            }
        }

        public SignatureModel Signature
        {
            get { return _signature; }
            set
            {
                _signature = value;
                OnPropertyChanged();
            }
        }

        public SubjectModel Subject
        {
            get { return _subject; }
            set
            {
                _subject = value;
                OnPropertyChanged();
                OnPropertyChanged("IsVisible");
            }
        }

        public ObservableCollection<object> Certs
        {
            get { return _certs; }
            set
            {
                _certs = value;
                OnPropertyChanged();
            }
        }
        public void AddCertificate(object cert)
        {
            _certs.Add(cert);
            OnPropertyChanged("Certs");
        }
        public void Clear()
        {
            _resultText = null;
            if (_signature != null)
            {
                _signature.Clear();
            }
            if (_subject != null)
            {
                _subject.Clear();
            }
            if (_certs != null)
            {
                _certs.Clear();
            }
            _resultOk = false;
            HasVaccinations = false;
            HasTest = false;
            HasRecovered = false;
            OnPropertyChanged("IsVisible");
            OnPropertyChanged("ResultText");
            OnPropertyChanged("Signature");
            OnPropertyChanged("Subject");
        }

    }

   
}