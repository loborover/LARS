import sys

def read_file(path):
    try:
        with open(path, 'r', encoding='cp949') as f:
            print(f.read())
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    if len(sys.argv) > 1:
        read_file(sys.argv[1])
    else:
        print("Usage: python read_vba.py <filepath>")
