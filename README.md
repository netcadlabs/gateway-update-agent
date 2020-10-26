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
# Kurulum sonrası servisin kontrolü için kullanılabilecek komutlar:
sudo systemctl status GatewayUpdateAgent  # servis çalışma durumunu ve son logları gösterir
sudo systemctl stop GatewayUpdateAgent  # durdurur
sudo systemctl start GatewayUpdateAgent.  # başlatır
sudo systemctl restart GatewayUpdateAgent.  # yeniden başlatır
```

## Ayarlar

*/etc/GatewayUpdateAgent/**GatewayUpdateAgent.conf*** ayar dosyası:

```json
{
  "Hostname" : "https://gateway.netcad.com",
  "Token": "bG4IO6ICHIOl1obCB2Mu",
  "IntervalInMinutes": 30,
      
  "Apps": [
      {
          "App" :"default",
          "ExtensionFolder" : "/var/lib/thingsboard_gateway/extensions/",
          "ConfigFolder" : "/etc/thingsboard-gateway/config" ,
          "YamlCollectionName" : "connectors",
          "YamlFileName" : "/etc/thingsboard-gateway/config/tb_gateway.yaml",          
          "RestartServices" : [ "thingsboard-gateway.service"]          
      },    
      {
          "App" :"ndu_gate",
          "ExtensionFolder" : "/var/lib/ndu_gate/runners/",
          "ConfigFolder" : "/etc/ndu-gate/config" ,
          "YamlCollectionName" : "runners",
          "YamlFileName" : "/etc/ndu-gate/config/ndu_gate.yaml",          
          "RestartServices" : [ "thingsboard-gateway.service", "ndu-gate.service"]
      }
  ]
}
```

* ***Hostname:*** Update API
* ***IntervalInMinutes:*** Güncelleme kontrolü kaç dakikada bir yapılsın. Ondalıklı değer olabilir.
* ***ExtensionFolder:*** Paketlerin kurulacağı dizin.
* ***ConfigFolder:*** Config (.json) dosyalarının kopyalanacağı dizin.
* ***YamlFileName:*** Thingsboard yaml dosyası tam yolu.

## Güncelleme / Kurulum

* GW Update Agent, belirli aralıklarla Update API'ye kendisinde kurulu olması gerekenlerin neler olduğunu sorgular. Sorguyu *device token* ile yapar. Update API bu token için belirlenmiş kurulumların ne olduğunu bilgisini json olarak döner:

```json
[
    {
        "type": "sigara",
        "uuid": "uuid-1",
        "url": "https://.../config.json.zip",
        "category": "CONFIG",
        "version": 23,
        
        "custom_app":
        {
          "ExtensionFolder" : "/<custom>/",
          "ConfigFolder" : "/<custom>/config" ,
          "YamlCollectionName" : "<custom>",
          "YamlFileName" : "/<custom>/<custom>.yaml",          
          "RestartServices" : [ "<custom service 1>", "<custom service 2>"]
        }
    },
    {
        "type": "sigara",
        "uuid": "uuid-2",
        "url": "https://.../sigara-framework.zip",
        "category": "PACKAGE",
        "version": 5,
        
        "app" : "ndu_gate"
    },
    {
        "type": "sigara",
        "uuid": "uuid-3",
        "url": "https://.../sigara-model-data.zip",
        "category": "PACKAGE",
        "version": 2,
    },
    {
        "type": "sigara",
        "uuid": "uuid-4",
        "url": "https://.../sigara-kamera-komutları.zip",
        "category": "COMMAND",
        "version": 1,
    },
    {
        "type": "sigara",
        "uuid": "uuid-5",
        "url": "https://.../sigara-model-komutları.zip",
        "category": "COMMAND",
        "version": 1,
    }
```
Custum config response örneği:
```json
[
    {
        "type": "sigara",
        "url": "https://.../custom_config.json",
        "category": "CUSTOM_CONFIG",
        "version": 1,
        
        "app" : "ndu_gate"
    },
    {
        "type": "sosyal mesafe",
        "url": "https://.../custom_config.json",
        "category": "CUSTOM_CONFIG",
        "version": 1,
        
        "custom_app":
        {
          "ExtensionFolder" : "/<custom>/",
          "ConfigFolder" : "/<custom>/config" ,
          "YamlCollectionName" : "<custom>",
          "YamlFileName" : "/<custom>/<custom>.yaml",          
          "RestartServices" : [ "<custom service 1>", "<custom service 2>"]
        }
    }
]
```

* ***type:*** Bir modüle ait unique bir isim olarak düşünebiliriz. Bir *type* için birden fazla paket, config, command vs. olabilir.
  * *tb_gateway.yaml* içerisine eklenecek connector elemanının *type* değeri olarak kullanılır.
  * *extensions* dizini altına yaratılacak klasör adı olarak kullanılır.
* ***id:*** Unique string.
  * Yukarıdaki json örneğini inceleyelim: "sigara" isimli modüle ait birden fazla paket tanımlandığını görüyoruz.
    * *uuid-2* kullanılan framework için gereken dosyaları içeriyor.
    * *uuid-3* model dosyaları içeriyor. "sigara" modülü için bir model değişikliği yaptığımız zaman, sadece *uuid-3* için bir yeni versiyon çıkarmak yeterli olacak, gateway'in modül için kullanılan framework paketini tekrar indirmesine gerek olmayacak.
* ***url:*** İndirilecek olan veri.
* ***category:*** *Url* içeriğinin ne olacağı ifade eder.
  * **CONFIG:** Url içeriği bir zip dostasıdır. Zip dosyası içerisinde tek json dosyası vardır. Bu dosya *ayarlar:ConfigFolder/**\<type>.json*** dosyası olarak kaydedilir.
  * **PACKAGE:** Url içeriği bir zip dosyasıdır. Zip dosyası içerisinde:
    * *Root* dizinde bir **info.json** dosyası olabilir. Örnek:

      ```json
      {
          "connector_config": {
            "class": "SigaraConnector"
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

      * **connector_config**: *tb_gateway.yaml* dosyasına yazılacak connector içerisine eklenmesi istenen özniteliklerdir.
      * **copy:** Zip dosyası içeriğinden özel bir dizine kopyalanması istenen dosya ve klasörler *source - destination* şeklinde tanımlanabilir.
    * Zip dosyası içerisinde, *copy* tanımı yapılmamış dosya ve klasör varsa,*ayarlar:ExtensionFolder/**\<type>*** dizinine kopyalanır.  
  * **COMMAND:** Url içeriği bir zip dosyasıdır. Zip dosyası içerisinde, *root* dizinde:
    * **pre_install.sh** isimli bir dosya varsa, Thingsboard servisi **durdurulmadan önce** çalıştırılır.
    * **install.sh** isimli bir dosya varsa, Thingsboard servisi **durdurulduktan sonra** çalıştırılır.
    * **post_install.sh** isimli bir dosya varsa, Thingsboard servisi **çalıştırıldıktan sonra** çalıştırılır.
    * **uninstall.sh** isimli bir dosya varsa, bu elemanın uninstall işlemi için çalıştırılır.
  * **CUSTOM_CONFIG:** Url içeriği json'dır. *ayarlar:ConfigFolder/**\<type>_custom.json*** dosyası olarak kaydedilir


## Çalışma Detayları

* GW Update Agent, API'den gelen bilgiyi şu şekilde değerlendirir:
  * API'nin döndüğü listede bulunan ancak gateway'e henüz kurulmamış olan elemanları indirir; indirmesi başarılı olanları kurar.
  * API'nın döndüğü listede bulunan, gateway'e kurulmuş ve versiyonu farklı olan elemanları önce kaldırır, sonra kurar.
  * API'nin döndüğü listede bulunmayan ancak gateway'e kurulmuş olan elemanlar için:
    * **CONFIG** elemanlar için oluşturulan json dosyalarını aktif olmayan elemanlar için kullanılan bir dizine taşır. Yani config deaktive edilir.
    * **PACKAGE** içeriği ile gelen tim dosyalar aktif olmayan elemanlar için kullanılan bir dizine taşınır. Yani package deaktive edilir.
    * **COMMAND** içeriğinde **uninstall.sh** dosyası varsa çalıştırılır. Command'e ait tutulan birşey varsa silinir. Yani command elemanları deaktive edilmez, kaldırılır.
    * **CUSTOM_CONFIG** elemanlar için oluşturulan json dosyalarını aktif olmayan elemanlar için kullanılan bir dizine taşır. Yani custom_config deaktive edilir.
  * İnaktif hale gelmiş bir eleman API'nin döndüğü listede yeniden kurulmak istenirse.
    * Versiyon aynı değilse inaktif olarak tutulan dosyalar silinir, eleman yeniden indirilir.
    * Versiyon aynıysa, eleman indirilmez, inaktif olarak tutulan dosyalar kullanılarak yeniden kurulum yapılır.

* Install veya uninstall yapılacaksa, Thingsboard servisi işlem öncesi durdurulur ve işlem sonrası tekrar çalışılır. Ardından Update API'ye güncel durum bildirilir. Hata oluşmuşsa bildirilir.
    
### Notlar

* Paketleme işleri için kullanılan kütüphane: <https://github.com/qmfrederik/dotnet-packaging>
* **.deb** dosyalarını otomatik hazırlamak için proje içerisindeki ```pack.sh``` dosyası çalıştırılabilir. Bu script "pack" isimli bir kasör içerisine kurulum dosyalarını oluşturacaktır.
