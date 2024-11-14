const express = require("express");
const app = express();
const logger = require('pino')()

const PORT = process.env.PORT || "8080";

app.get("/hello", (_, res) => {
  logger.info('/hello endpoint invoked, sending response');
  res.status(200).send("Hello, World!");
});

app.listen(parseInt(PORT, 10), () => {
  logger.info(`Listening for requests on http://localhost:${PORT}`);
});
