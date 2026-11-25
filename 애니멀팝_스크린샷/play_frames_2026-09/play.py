from google.oauth2 import service_account
from googleapiclient.discovery import build
from googleapiclient.errors import HttpError
SA="/Users/yong/.config/gcloud/legacy_credentials/play-publisher@my-popol-play-59327.iam.gserviceaccount.com/adc.json"
creds=service_account.Credentials.from_service_account_file(SA,scopes=["https://www.googleapis.com/auth/androidpublisher"])
svc=build("androidpublisher","v3",credentials=creds,cache_discovery=False)
