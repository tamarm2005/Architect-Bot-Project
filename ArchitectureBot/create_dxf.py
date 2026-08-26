import ezdxf
from flask import Flask, request, send_file
from flask_cors import CORS
import io

app = Flask(__name__)
CORS(app)

@app.route('/process-dxf', methods=['POST'])
def process_dxf():
    try:
        if 'file' not in request.files:
            return "No file part", 400
        
        file = request.files['file']
        
        file_content = file.read()
        doc = ezdxf.readbytes(file_content)
        msp = doc.modelspace()

        walls = msp.query('LINE[layer=="WALLS"]')
        
        if not walls:
            print("Warning: No walls found in layer 'WALLS'")
        
        for line in walls:
            start = line.dxf.start
            end = line.dxf.end
            msp.add_linear_dim(base=(start.x, start.y + 50), p1=start, p2=end).render()

        out_buffer = io.BytesIO()
        doc.write(out_buffer)
        out_buffer.seek(0)
        
        return send_file(
            out_buffer,
            mimetype="application/dxf",
            as_attachment=True,
            download_name="output_with_measures.dxf"
        )

    except Exception as e:
        print(f"Error occurred: {e}") 
        return str(e), 500

@app.route('/chat', methods=['POST'])
def chat():
    try:
        data = request.json
        user_message = data.get('text', '')
        
        print(f"User asked: {user_message}") 

        bot_reply = f"קיבלתי את השאלה שלך: '{user_message}'. אני בודק את התקנות הרלוונטיות..."

        return {"reply": bot_reply}, 200

    except Exception as e:
        print(f"Chat error: {e}")
        return {"reply": "מצטער, קרתה שגיאה בעיבוד השאלה."}, 500

if __name__ == '__main__':
    app.run(port=5000, debug=True)