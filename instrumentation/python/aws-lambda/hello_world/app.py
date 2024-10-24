import json
import requests
import logging

logger = logging.getLogger(__name__)
logger.setLevel("INFO")

def lambda_handler(event, context):

    logger.info('In lambda_handler, about to get the IP address...')

    try:
        ip = requests.get("http://checkip.amazonaws.com/")
    except requests.RequestException as e:
        logger.error(e)
        raise e

    logger.info('Successfully got the IP address, returning a response.')

    return {
        "statusCode": 200,
        "body": json.dumps({
            "message": "hello world",
             "location": ip.text.replace("\n", "")
        }),
    }
