from flask import Flask, request
import logging

app = Flask(__name__)
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

@app.route("/hello")
def hello_world():

    logger.info("Handling the /hello request")
    return "Hello, World!"
