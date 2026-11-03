dotnet new web -n ServiceApi
cd ServiceApi
dotnet add package Microsoft.Extensions.Hosting.WindowsServices



# ดูทั้งหมด
curl -H "X-Api-Key: xxx" http://127.0.0.1:5050/services

# start
curl -H "X-Api-Key: xxx" -X POST http://127.0.0.1:5050/services/<service_name>/start

# stop
curl -H "X-Api-Key: xxx" -X POST http://127.0.0.1:5050/services/<service_name>/stop

# delete
curl -H "X-Api-Key: xxx" -X DELETE http://127.0.0.1:5050/services/<service_name>

# create (ใส่ path ของ exe ตัวจริง)
curl -H "X-Api-Key: xxx" -X POST http://127.0.0.1:5050/services -H "Content-Type: application/json" -d "{\"name\":\"<service_name>\",\"path\":\"C:\\\\apps\\\\<service_name>.exe\"}"

dotnet publish -c Release -o ./publish