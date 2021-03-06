﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Pyhh.Browsing
{
    public enum Countries
    {
        UN = 0,
        RU = 1,
        CN = 2,
        US = 4,
        DE = 8,
        FR = 16,
        IE = 32,
        GB = 64,
        SG = 128
    }
    public class GeoDetector
    {
        public GeoDetector(string providerUrl = "http://ip-api.com")
        {
            ProviderUrl = providerUrl;
        }

        private string ProviderUrl { get; set; }

        public async Task<Countries> GetCountryAsync()
        {
            Countries result = Countries.UN;

            GeoData data = await GetGeoData();

            if (data != null)
            {
                bool success = Enum.TryParse(data.CountryCode, out Countries parseResult);

                if (success)
                {
                    result = parseResult;
                }
            }

            return result; 
        }

        private async Task<GeoData> GetGeoData()
        {
            GeoData result = null;

            try
            {
                using (HttpClient client = Utils.GetHttpClient("Pyhh.Browsing/1.0 (2412719@mail.ru)", 60))
                {
                    client.BaseAddress = new Uri(ProviderUrl);
                    HttpResponseMessage response = await client.GetAsync("/json");

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        GeoData data = await response.Content.ReadAsJsonAsync<GeoData>();

                        if (data != null)
                        {
                            result = data;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Error getting geodata, request status code is " + response.StatusCode);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error getting geodata: " + e);
            }

            return result;
        }
    }
}
