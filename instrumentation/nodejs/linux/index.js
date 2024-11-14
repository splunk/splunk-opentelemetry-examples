const express = require("express");
const app = express();

const PORT = process.env.PORT || "8080";

app.get("/hello", (_, res) => {
  res.status(200).send("Hello, World!");
});

app.listen(parseInt(PORT, 10), () => {
  console.log(`Listening for requests on http://localhost:${PORT}`);
});
