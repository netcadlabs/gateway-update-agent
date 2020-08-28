# gateway-update-agent

Wiki: <https://wiki.netcad.com.tr/pages/viewpage.action?pageId=229913061>

## Kurulum

Güncel sürüm: <https://github.com/netcadlabs/gateway-update-agent/releases> 

* İşletim sistemine uygun .deb dosyası şu komutla kurulabilir:
  
  ```bash
  sudo dpkg -i pack/GatewayUpdateAgent.<version><platform>.deb
  ```
  
  * Ayar dosyası: **/etc/GatewayUpdateAgent/GatewayUpdateAgent.conf**
  * Servis dosyası: **/etc/systemd/system/GatewayUpdateAgent.service**
  * Log dosyası: **/var/log/GatewayUpdateAgent/logs.txt**

```bash
# Kurum sonrası servisin kontrolü için:
sudo systemctl status GatewayUpdateAgent  # servis çalışma durumunu ve son logları gösterir
sudo systemctl stop GatewayUpdateAgent  # durdurur
sudo systemctl start GatewayUpdateAgent.  # başlatır
sudo systemctl restart GatewayUpdateAgent.  # yeniden başlatır
```

## Çalışma

* Belirli aralıklarla Update API'ye güncelleme olup olmadığını sorar.
* Device token ve hali hazırda kurulu olan connector id ve versiyonlarını göndererek Gateway'de yapılması vereken bir kurma veya kaldırma işi var mı kararını verir.

### Ayarlar

```json
{
  "Hostname" : "https://nduupdateserver.netcad.com",
  "IntervalInMinutes": 10,
  "ExtensionFolder" : "/var/lib/thingsboard_gateway/extensions/",
  "ConfigFolder" : "/etc/thingsboard-gateway/config",
  "YamlFileName" : "/etc/thingsboard-gateway/config/tb_gateway.yaml"
}
```

* ***Hostname:*** Update API
* ***IntervalInMinutes:*** Güncelleme kontrolü kaç dakikada bir yapılsın. Ondalıklı değer olabilir.
* ***ExtensionFolder:*** Paketlerin kurulacağı dizin.
* ***ConfigFolder:*** Config dosyalarının kopyalanacağı dizin.
* ***YamlFileName:*** Thingsboard yaml dosyası tam yolu.

### Güncelleme / Kurulum İşleri

#### Terminoloji

* ***Bundle:*** Gateway'e kurulacak olan modüllerdir. Bir bundle'ın **type** bilgisi olmak zorundadır. *****Type metni klasör ve dosya adına uygun karakterlerden oluşmalıdır.***** Bir bundle, **config** ve/veya **pack** içeriğine sahip olabilir.
* ***Config:*** ConfigFolder dizinine "config_\<type>.json" formatında kopyalanacak olan dosyadır.
* ***Pack:*** ExtensionFolder dizini altında \<type> adındaki klasöre extract edilecek *zip* içeriğidir.
  * Zip dosyası içerisinde *root* dizinde bir **info.json** dosyası **olmak zorundadır**.

```json
{
    "connector_config": {
        "class": "Test1Connector_v1"
    },
    "copy": [
      {
              "source": "test_copy_folder/f1",
            "destination": "/etc/test_gua_v1/f1"
      },
      {
            "source": "test_copy_folder/file2.txt",
            "destination": "/etc/test_gua_v1/test_copy_folder_for_file2"
       }
    ]
}
```

* **connector_config / class** bilgisi olmak zorundadır. connector_config içerisinde custom property'ler eklenebilir, bunlar yaml dosyasına işlenecektir.
* Zip içerisindeki istenen dosyalar veya klasörler için copy tanımı yapılabilir. Bu sayede ExtensionFolder dışında bir yere kopyalanmak istenen dosya veya klasörler ayarlanabilir.
  * *source*, zip dosyası içerisinde olan dosya veya klasördür.
  * *destination*, hedef **klasör**dür.

#### Güncelleme/Kurma adımları

GW Update Agent, Update API'den kendisinde kurulu olması gereken bundle bilgisini sunucudan sorgular. API bir array döner. Örnek:

```json
[
    {
        "type" : "sigara",
        "id" : "uuid", 
        "url": "https://raw.githubusercontent.com/korhun/nduagent/master/test/config.json",
        "category": "CONFIG",
        "version": 23,
    },
    {
        "type" : "sigara",
        "id" : "uuid",
        "url": "https://raw.githubusercontent.com/korhun/nduagent/master/test/sigara.zip",
        "category": "PACKAGE",
        "version": 5,
    },
    {
        "type" : "sigara",
        "id" : "uuid",
        "url": "https://raw.githubusercontent.com/korhun/nduagent/master/test/sigara-model-data.zip",
        "category": "PACKAGE",
        "version": 2,
    }
]
```
***type:*** onnector name
***id:*** unique name
***category:*** //CONFIG, PACKAGE

* GW Update Agent,
  * Daha önceden kurulmuş ancak bu listede bulunmayan pack ve config'leri siler.
  * Bu listede bulunan, daha önceden kurulmamış pack ve configleri kurar. Daha önceden kurulmuş ancak versiyon değeri daha düşük olan pack ve config'leri siler, yenilerini kurar.
  * Kurulum yapılacaksa, thingsboard servisi işlem öncesi durdurulur ve işlem sonrası tekrar çalışılır. Ardından Update API'ye güncel durum bildirilir. Hata oluşmuşsa bildirilir.

### Çalışma Detayları

* UpdatePackage(Bundle) sorgusu yapılır.
  * Bu sorgu sonucu UpdatePackage listesi döner. Bu listenin içerisinde her pakete ait **type**, **name** ve versiyon bilgileri bulunur.
  * UpdatePackage içerisinde hem source hem de config indirme url adresleri bulunur.
* Her bir UpdatePackage için aşağıdaki işlemler yapılır.
  * tb_gateway.yaml dosyası okunur.
  * UpdatePackage source dosyası versiyonu daha önce kurulmamış ya da kurulu olandan daha yeni ise  indirilir.
    * İndirilen source dosyası içindeki **info.json** içerisinde confige eklenecek olan **class** değeri okunur.
    * Dosyanın içindeki tb/extension klasörü içindeki dosyalar **/var/lib/thingsboard_gateway/extensions/\<type>** klasörü içerisine kopyalanır. type değeri UpdatePackage içinde type değeridir.
    * Kopyalama işlemi bittikten sonra tb_gateway.yaml dosyasında connectors kısmına aşağıdaki gibi yeni bir eleman eklenir/güncellenir.
    * Güncelleme işleminde **pack_versiyon** ve değişmişse **class** değeri güncellenir.
  
```yaml
-
 name: <type> Connector
 type: <type>
 configuration: <type>.json
 class: <class> //info.json'dan okunan değer.
 version_<conf-id>: <<agent version>#<pack-id>> 
 version_<pack1-id>: <>
 version_<pack-2-id>: <>
 version_<pack-N-id>: <>
```
  
* UpdatePackage config download url üzerin den config dosyası indirilir.
  * İndirilen config dosyası içeriği json olacaktır. Bu dosyanın içeriği **/etc/thingsboard-gateway/config** klasörü içerisinde **\<type>.json** olarak kaydedilecektir.

### Notlar

* Paketleme işleri için kullanılan kütüphane: <https://github.com/qmfrederik/dotnet-packaging>
* **.deb** dosyalarını otomatik hazırlamak için proje içerisindeki ```pack.sh``` dosyası çalıştırılabilir. Bu script "pack" isimli bir kasör içerisine kurulum dosyalarını oluşturacaktır.
